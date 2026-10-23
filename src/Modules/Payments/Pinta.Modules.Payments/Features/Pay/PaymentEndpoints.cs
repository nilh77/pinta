using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Payments.Contracts;
using Pinta.Modules.Payments.Data;
using Pinta.Modules.Payments.Domain;
using Pinta.Modules.Payments.Stripe;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;
using Pinta.SharedKernel.Idempotency;
using Pinta.SharedKernel.Time;

namespace Pinta.Modules.Payments.Features.Pay;

public sealed record CreatePaymentRequest(PaymentType Type);

/// <summary>What the payment screen needs to mount the Payment Element (screen 11).</summary>
public sealed record CreatePaymentResponse(
    Guid PaymentId,
    PaymentType Type,
    long AmountCents,
    string ClientSecret,
    string PublishableKey,
    IReadOnlyList<PaymentMethod> Methods,
    bool MbWayLimitWarning);

public sealed record MultibancoDto(string Entity, string Reference, DateTimeOffset? ExpiresAt);

public sealed record RefundDto(Guid Id, long AmountCents, RefundStatus Status, string? Reason, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);

public sealed record PaymentDto(
    Guid Id,
    PaymentType Type,
    PaymentStatus Status,
    PaymentMethod? Method,
    long AmountCents,
    long RefundedCents,
    MultibancoDto? Multibanco,
    string? Error,
    string? ReceiptNumber,
    DateTimeOffset? PaidAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<RefundDto> Refunds)
{
    internal static PaymentDto From(Payment p) => new(
        p.Id, p.Type, p.Status, p.Method, Money.ToCents(p.Amount), Money.ToCents(p.Refunded),
        p.MultibancoReference is null ? null : new MultibancoDto(p.MultibancoEntity ?? "", p.MultibancoReference, p.MultibancoExpiresAt),
        p.Error, p.ReceiptNumber, p.PaidAt, p.CreatedAt,
        [.. p.Refunds.OrderBy(r => r.CreatedAt).Select(r => new RefundDto(r.Id, Money.ToCents(r.Amount), r.Status, r.Reason, r.CreatedAt, r.CompletedAt))]);
}

internal sealed class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator() => RuleFor(r => r.Type).IsInEnum();
}

/// <summary>
/// POST /api/jobs/{id}/payments, GET /api/jobs/{id}/payments and GET /api/admin/jobs/{id}/payments (C5.3, A3.5). The
/// amount always comes from the server; an open PaymentIntent of the same job and type is reused, so going back never
/// charges twice.
/// </summary>
internal static class PaymentEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var client = app.MapGroup("/api/jobs/{id:guid}/payments").WithTags("Payments")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Client));
        client.MapPost("/", CreateAsync)
            .WithValidation<CreatePaymentRequest>()
            .RequireIdempotencyKey<CreatePaymentRequest>()
            .WithName("CreatePayment")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone)
            .ProducesProblem(StatusCodes.Status502BadGateway);
        client.MapGet("/", ListForClientAsync).WithName("ListJobPayments").ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/admin/jobs/{id:guid}/payments", ListForAdminAsync)
            .WithTags("Admin jobs")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin))
            .WithName("ListAdminJobPayments");
    }

    private static async Task<Results<Ok<CreatePaymentResponse>, ProblemHttpResult>> CreateAsync(
        Guid id, CreatePaymentRequest request, HttpContext http, IPayableJobs jobs, PaymentsDbContext db, IStripeGateway stripe,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var job = await jobs.FindAsync(id, cancellationToken);
        if (job is null || job.ClientId != http.User.GetUserId())
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        var existing = await db.Payments.Where(p => p.JobId == job.JobId && p.Type == request.Type).ToListAsync(cancellationToken);
        if (existing.Any(p => p.PaidAt != null))
        {
            return ApiProblem.Create(StatusCodes.Status422UnprocessableEntity, ErrorCodes.PaymentAlreadyMade);
        }

        var (amountCents, problem) = Amount(job, request.Type, BusinessDays.Today(clock));
        if (problem is not null)
        {
            return problem;
        }

        if (!stripe.IsConfigured)
        {
            return ApiProblem.Create(StatusCodes.Status502BadGateway, ErrorCodes.PaymentProviderError);
        }

        var now = clock.GetUtcNow();
        var open = existing.FirstOrDefault(p => p.IsOpen && Money.ToCents(p.Amount) == amountCents);
        foreach (var stale in existing.Where(p => p.IsOpen && p != open))
        {
            // The amount changed (a new price): the old intent can no longer be paid.
            await stripe.CancelPaymentIntentAsync(stale.StripePaymentIntentId, cancellationToken);
            stale.Status = PaymentStatus.Cancelled;
            stale.UpdatedAt = now;
        }

        StripeIntent intent;
        if (open is not null)
        {
            intent = await stripe.GetPaymentIntentAsync(open.StripePaymentIntentId, cancellationToken);
        }
        else
        {
            var paymentId = Guid.CreateVersion7();
            intent = await stripe.CreatePaymentIntentAsync(
                amountCents,
                [.. PaymentRules.AllowedMethods(amountCents).Select(PaymentRules.StripeType)],
                new Dictionary<string, string> { ["paymentId"] = paymentId.ToString(), ["jobId"] = job.JobId.ToString(), ["jobCode"] = job.Code },
                $"payment-{paymentId}",
                cancellationToken);
            open = new Payment
            {
                Id = paymentId,
                JobId = job.JobId,
                ClientId = job.ClientId,
                Type = request.Type,
                Amount = Money.FromCents(amountCents),
                Status = PaymentStatus.Pending,
                StripePaymentIntentId = intent.Id,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Payments.Add(open);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new CreatePaymentResponse(
            open.Id, open.Type, amountCents, intent.ClientSecret, stripe.PublishableKey!, PaymentRules.AllowedMethods(amountCents),
            PaymentRules.MbWayWarning(amountCents)));
    }

    /// <summary>What the job owes for <paramref name="type"/> now, or why it cannot be paid.</summary>
    internal static (long AmountCents, ProblemHttpResult? Problem) Amount(PayableJob job, PaymentType type, DateOnly today)
    {
        static (long, ProblemHttpResult?) Fail(int status, string code) => (0, ApiProblem.Create(status, code));

        switch (type)
        {
            case PaymentType.Deposit:
                if (job.Status == JobStatus.Expired || (job.Status == JobStatus.Quoted && job.QuoteValidUntil < today))
                {
                    return Fail(StatusCodes.Status410Gone, ErrorCodes.QuoteExpired);
                }

                return job.Status == JobStatus.Quoted && job.DepositCents is { } deposit
                    ? (deposit, null)
                    : Fail(StatusCodes.Status409Conflict, ErrorCodes.InvalidTransition);
            case PaymentType.Balance:
                return job.Status == JobStatus.Finished && job.BalanceCents is { } balance
                    ? (balance, null)
                    : Fail(StatusCodes.Status409Conflict, ErrorCodes.InvalidTransition);
            case PaymentType.SiteVisit:
                return job.Status == JobStatus.SiteVisit && job.SiteVisitPriceCents > 0
                    ? (job.SiteVisitPriceCents, null)
                    : Fail(StatusCodes.Status409Conflict, ErrorCodes.InvalidTransition);
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    private static async Task<Results<Ok<IReadOnlyList<PaymentDto>>, ProblemHttpResult>> ListForClientAsync(
        Guid id, HttpContext http, IPayableJobs jobs, PaymentsDbContext db, CancellationToken cancellationToken)
    {
        var job = await jobs.FindAsync(id, cancellationToken);
        return job is null || job.ClientId != http.User.GetUserId()
            ? ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound)
            : TypedResults.Ok(await ListAsync(db, id, cancellationToken));
    }

    private static async Task<Ok<IReadOnlyList<PaymentDto>>> ListForAdminAsync(Guid id, PaymentsDbContext db, CancellationToken cancellationToken) =>
        TypedResults.Ok(await ListAsync(db, id, cancellationToken));

    private static async Task<IReadOnlyList<PaymentDto>> ListAsync(PaymentsDbContext db, Guid jobId, CancellationToken cancellationToken) =>
        [.. (await db.Payments.AsNoTracking().Include(p => p.Refunds).Where(p => p.JobId == jobId).OrderBy(p => p.CreatedAt).ToListAsync(cancellationToken))
            .Select(PaymentDto.From)];
}
