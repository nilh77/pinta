using FluentValidation;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Notifications.Data;
using Pinta.Modules.Notifications.Domain;
using Pinta.Modules.Notifications.Features.Push;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Notifications.Features.Notices;

/// <summary>A notice in the bell; the web writes its text from <see cref="Kind"/> and <see cref="Data"/>.</summary>
public sealed record NotificationDto(
    Guid Id, NotificationKind Kind, Guid? JobId, string? JobCode, IReadOnlyDictionary<string, string> Data, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

public sealed record UnreadCount(int Count);

public sealed record PushPublicKey(string PublicKey);

public sealed record PushSubscriptionRequest(string Endpoint, string P256dh, string Auth);

internal sealed class PushSubscriptionRequestValidator : AbstractValidator<PushSubscriptionRequest>
{
    public PushSubscriptionRequestValidator()
    {
        RuleFor(r => r.Endpoint).NotEmpty().MaximumLength(1000).Must(e => Uri.TryCreate(e, UriKind.Absolute, out var uri) && uri.Scheme == "https");
        RuleFor(r => r.P256dh).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Auth).NotEmpty().MaximumLength(100);
    }
}

/// <summary>The bell and this device's push (docs/api.md, «Avisos»): every user sees and marks only their own.</summary>
internal static class NotificationEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var notifications = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();
        notifications.MapGet("", ListAsync).WithName("ListNotifications");
        notifications.MapGet("/unread-count", UnreadAsync).WithName("UnreadNotifications");
        notifications.MapPost("/{id:guid}/read", ReadAsync).WithName("ReadNotification").ProducesProblem(StatusCodes.Status404NotFound);
        notifications.MapPost("/read-all", ReadAllAsync).WithName("ReadAllNotifications");

        var push = app.MapGroup("/api/push").WithTags("Notifications").RequireAuthorization();
        push.MapGet("/public-key", PublicKey).WithName("PushPublicKey").ProducesProblem(StatusCodes.Status404NotFound);
        push.MapPost("/subscriptions", SubscribeAsync).WithValidation<PushSubscriptionRequest>().WithName("SubscribePush");
        push.MapDelete("/subscriptions", UnsubscribeAsync).WithName("UnsubscribePush");
    }

    private static IQueryable<Notification> Mine(NotificationsDbContext db, HttpContext http)
    {
        var userId = http.User.GetUserId()!.Value;
        return db.Notifications.Where(n => n.UserId == userId);
    }

    private static async Task<Ok<Paged<NotificationDto>>> ListAsync(
        int? page, int? pageSize, HttpContext http, NotificationsDbContext db, CancellationToken cancellationToken)
    {
        var (p, size) = Paged<NotificationDto>.Normalize(page, pageSize);
        var mine = Mine(db, http).AsNoTracking();
        var total = await mine.CountAsync(cancellationToken);
        var items = await mine.OrderByDescending(n => n.CreatedAt).Skip((p - 1) * size).Take(size).ToListAsync(cancellationToken);
        return TypedResults.Ok(new Paged<NotificationDto>(
            [.. items.Select(n => new NotificationDto(n.Id, n.Kind, n.JobId, n.JobCode, n.Data, n.CreatedAt, n.ReadAt))], p, size, total));
    }

    private static async Task<Ok<UnreadCount>> UnreadAsync(HttpContext http, NotificationsDbContext db, CancellationToken cancellationToken) =>
        TypedResults.Ok(new UnreadCount(await Mine(db, http).CountAsync(n => n.ReadAt == null, cancellationToken)));

    private static async Task<Results<NoContent, ProblemHttpResult>> ReadAsync(
        Guid id, HttpContext http, NotificationsDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var notification = await Mine(db, http).SingleOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (notification is null)
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        notification.ReadAt ??= clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> ReadAllAsync(HttpContext http, NotificationsDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await Mine(db, http).Where(n => n.ReadAt == null).ExecuteUpdateAsync(n => n.SetProperty(x => x.ReadAt, now), cancellationToken);
        return TypedResults.NoContent();
    }

    private static Results<Ok<PushPublicKey>, ProblemHttpResult> PublicKey(PushOptions options) =>
        options.Enabled
            ? TypedResults.Ok(new PushPublicKey(options.PublicKey!))
            : ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);

    /// <summary>Idempotent per endpoint: the same device signing in as someone else moves to that user.</summary>
    private static async Task<NoContent> SubscribeAsync(
        PushSubscriptionRequest request, HttpContext http, NotificationsDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var userId = http.User.GetUserId()!.Value;
        var userAgent = http.Request.Headers.UserAgent.ToString();
        var existing = await db.PushSubscriptions.SingleOrDefaultAsync(s => s.Endpoint == request.Endpoint, cancellationToken);
        if (existing is null)
        {
            db.PushSubscriptions.Add(new PushSubscription
            {
                UserId = userId,
                Endpoint = request.Endpoint,
                P256dh = request.P256dh,
                Auth = request.Auth,
                UserAgent = userAgent.Length > 300 ? userAgent[..300] : userAgent,
                CreatedAt = clock.GetUtcNow(),
            });
        }
        else
        {
            existing.UserId = userId;
            existing.P256dh = request.P256dh;
            existing.Auth = request.Auth;
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> UnsubscribeAsync(string endpoint, HttpContext http, NotificationsDbContext db, CancellationToken cancellationToken)
    {
        var userId = http.User.GetUserId()!.Value;
        await db.PushSubscriptions.Where(s => s.UserId == userId && s.Endpoint == endpoint).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}

/// <summary>Daily: read notices older than 90 days are deleted (docs/data-model.md).</summary>
public sealed class NotificationCleanup(NotificationsDbContext db, TimeProvider clock)
{
    public const string JobId = "notification-cleanup";

    [AutomaticRetry(Attempts = 3)]
    public Task<int> RunAsync()
    {
        var cutoff = clock.GetUtcNow().AddDays(-90);
        return db.Notifications.Where(n => n.ReadAt != null && n.ReadAt < cutoff).ExecuteDeleteAsync();
    }
}
