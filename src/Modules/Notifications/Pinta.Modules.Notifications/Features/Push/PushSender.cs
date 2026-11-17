using System.Globalization;
using System.Resources;
using System.Text.Json;
using Hangfire;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using PushSubscription = Pinta.Modules.Notifications.Domain.PushSubscription;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Notifications.Data;
using Pinta.Modules.Notifications.Domain;

namespace Pinta.Modules.Notifications.Features.Push;

/// <summary>
/// VAPID keys for web push (docs/integrations.md, «Avisos y notificaciones push»): <c>Push:PublicKey</c>,
/// <c>Push:PrivateKey</c> and <c>Push:Subject</c>. Without both keys push is off and the bell still works.
/// </summary>
public sealed record PushOptions(string? PublicKey, string? PrivateKey, string Subject)
{
    public bool Enabled => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);

    public static PushOptions From(IConfiguration configuration) => new(
        configuration["Push:PublicKey"], configuration["Push:PrivateKey"], configuration["Push:Subject"] ?? "mailto:hello@pinta.pt");
}

/// <summary>What a push service answered: delivered, or the subscription no longer exists (404/410).</summary>
public enum PushOutcome
{
    Delivered,
    Gone,
}

/// <summary>Sends one encrypted message to a subscription; a double in the tests, like <c>IMailTransport</c>.</summary>
public interface IPushTransport
{
    Task<PushOutcome> SendAsync(PushSubscription subscription, string payload, CancellationToken cancellationToken);
}

internal sealed class WebPushTransport(HttpClient http, PushOptions options) : IPushTransport
{
    private readonly PushServiceClient client = new(http)
    {
        DefaultAuthentication = new VapidAuthentication(options.PublicKey, options.PrivateKey) { Subject = options.Subject },
    };

    public async Task<PushOutcome> SendAsync(PushSubscription subscription, string payload, CancellationToken cancellationToken)
    {
        var target = new Lib.Net.Http.WebPush.PushSubscription { Endpoint = subscription.Endpoint };
        target.SetKey(PushEncryptionKeyName.P256DH, subscription.P256dh);
        target.SetKey(PushEncryptionKeyName.Auth, subscription.Auth);
        try
        {
            await client.RequestPushMessageDeliveryAsync(target, new PushMessage(payload) { TimeToLive = 24 * 60 * 60 }, cancellationToken);
            return PushOutcome.Delivered;
        }
        catch (PushServiceClientException e) when (e.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
        {
            return PushOutcome.Gone;
        }
    }
}

/// <summary>
/// Hangfire job: sends one notice to every device of its user, in the user's language, and forgets the devices the
/// push service reports gone. Retried up to 5 times like the emails.
/// </summary>
public sealed class PushSender(NotificationsDbContext db, IUserDirectory users, IPushTransport transport)
{
    private static readonly ResourceManager Texts = new("Pinta.Modules.Notifications.Resources.Notices", typeof(PushSender).Assembly);

    [AutomaticRetry(Attempts = 5)]
    public async Task SendAsync(Guid notificationId)
    {
        var notification = await db.Notifications.AsNoTracking().SingleOrDefaultAsync(n => n.Id == notificationId);
        if (notification is null || notification.ReadAt is not null)
        {
            return;
        }

        var subscriptions = await db.PushSubscriptions.Where(s => s.UserId == notification.UserId).ToListAsync();
        if (subscriptions.Count == 0)
        {
            return;
        }

        var locale = (await users.FindAsync(notification.UserId))?.Locale ?? Pinta.SharedKernel.Locale.Pt;
        var payload = JsonSerializer.Serialize(new
        {
            title = notification.JobCode is null ? "Pinta" : $"Pinta · {notification.JobCode}",
            body = Text(notification, locale),
            url = UrlOf(notification, locale),
            tag = notification.Id,
        });

        foreach (var subscription in subscriptions)
        {
            if (await transport.SendAsync(subscription, payload, CancellationToken.None) == PushOutcome.Gone)
            {
                db.PushSubscriptions.Remove(subscription);
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>The notice's sentence in <paramref name="locale"/>, with its code and data filled in.</summary>
    public static string Text(Notification notification, Pinta.SharedKernel.Locale locale)
    {
        var culture = CultureInfo.GetCultureInfo(locale.ToString().ToLowerInvariant());
        var text = Texts.GetString(notification.Kind.ToString(), culture) ?? notification.Kind.ToString();
        text = text.Replace("{code}", notification.JobCode ?? "", StringComparison.Ordinal);
        foreach (var (key, value) in notification.Data)
        {
            text = text.Replace($"{{{key}}}", value, StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>Where the notice opens: the job in the area of its audience, or the matching admin screen.</summary>
    public static string UrlOf(Notification notification, Pinta.SharedKernel.Locale locale)
    {
        var language = locale.ToString().ToLowerInvariant();
        var path = (notification.Audience, notification.JobId) switch
        {
            (NoticeAudience.Admin, { } job) => $"/admin/trabajos/{job}",
            (NoticeAudience.Painter, { } job) => notification.Kind == NotificationKind.SiteVisitAssigned ? $"/pintor/visitas/{job}" : $"/pintor/trabajos/{job}",
            (NoticeAudience.Client, { } job) => $"/mis-trabajos/{job}",
            (NoticeAudience.Admin, null) => notification.Kind switch
            {
                NotificationKind.AccountDeletionRequested => "/admin/ajustes",
                NotificationKind.PainterDocumentsExpiring => "/admin/pintores",
                _ => "/admin/revision",
            },
            (NoticeAudience.Painter, null) => "/pintor/pagos",
            _ => "/avisos",
        };
        // A path, not a full URL: the service worker opens it on its own origin.
        return $"/{language}{path}";
    }
}
