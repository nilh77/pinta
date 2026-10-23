using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pinta.Modules.Finance.Contracts;
using Pinta.Modules.Finance.Data;
using Pinta.Modules.Finance.Domain;
using Pinta.Modules.Finance.Resources;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Painters.Contracts;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Events;
using Pinta.SharedKernel.Http;
using Pinta.SharedKernel.Time;

namespace Pinta.Modules.Finance.Features.Payouts;

public sealed record PayoutDto(
    Guid Id, Guid PainterId, Guid JobId, string Code, long AmountCents, PayoutStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt,
    string? Reference);

/// <summary>A5.4: the transfer was made on <see cref="PaidOn"/> (today or before) with this <see cref="Reference"/>.</summary>
public sealed record MarkPaidRequest(DateOnly PaidOn, string Reference);

internal sealed class MarkPaidRequestValidator : AbstractValidator<MarkPaidRequest>
{
    public MarkPaidRequestValidator() => RuleFor(r => r.Reference).NotEmpty().MaximumLength(100);
}

/// <summary>
/// P4.1: <see cref="PendingCents"/> is everything still owed; <see cref="PaidCents"/> what was paid in <see cref="Month"/>.
/// <see cref="Items"/> holds the pending payouts and the ones paid that month.
/// </summary>
public sealed record PainterPayouts(string Month, long PendingCents, long PaidCents, IReadOnlyList<PayoutDto> Items);

/// <summary>GET /api/painter/payouts (P4.1), the admin's list and «marcar como pagado» (A5.4).</summary>
internal static class PayoutEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/painter/payouts").WithTags("Painter")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Painter))
            .MapGet("", PainterAsync).WithName("ListMyPayouts").ProducesProblem(StatusCodes.Status404NotFound);
        var admin = app.MapGroup("/api/admin/payouts").WithTags("Admin finance")
            .RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
        admin.MapGet("", AdminAsync).WithName("ListPayouts");
        admin.MapPost("/{id:guid}/mark-paid", MarkPaidAsync).WithValidation<MarkPaidRequest>().WithName("MarkPayoutPaid")
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }

    /// <param name="month"><c>YYYY-MM</c>; the current month in Lisbon when left out.</param>
    private static async Task<Results<Ok<PainterPayouts>, ProblemHttpResult>> PainterAsync(
        string? month, HttpContext http, FinanceDbContext db, IPainterDirectory painters, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (!Months.TryParse(month, clock, out var first))
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["month"] = ["Use YYYY-MM."] });
        }

        var painter = await painters.FindByUserAsync(http.User.GetUserId()!.Value, cancellationToken);
        if (painter is null)
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        var (from, to) = Months.Bounds(first);
        var items = await db.PainterPayouts.AsNoTracking()
            .Where(p => p.PainterId == painter.Id
                && (p.Status == PayoutStatus.Pending || (p.Status == PayoutStatus.Paid && p.PaidAt >= from && p.PaidAt < to)))
            .OrderBy(p => p.Status).ThenByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(new PainterPayouts(
            Months.Format(first),
            items.Where(p => p.Status == PayoutStatus.Pending).Sum(p => Money.ToCents(p.Amount)),
            items.Where(p => p.Status == PayoutStatus.Paid).Sum(p => Money.ToCents(p.Amount)),
            [.. items.Select(ToDto)]));
    }

    private static async Task<Ok<IReadOnlyList<PayoutDto>>> AdminAsync(
        Guid? painterId, PayoutStatus? status, FinanceDbContext db, CancellationToken cancellationToken)
    {
        var query = db.PainterPayouts.AsNoTracking();
        if (painterId is { } id)
        {
            query = query.Where(p => p.PainterId == id);
        }

        if (status is { } s)
        {
            query = query.Where(p => p.Status == s);
        }

        return TypedResults.Ok<IReadOnlyList<PayoutDto>>([.. (await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken)).Select(ToDto)]);
    }

    /// <summary>
    /// A5.4: cash paid, so it stays allowed after the month is closed; the painter sees it paid in P4. Paid once: a second
    /// mark is <c>invalid_transition</c>.
    /// </summary>
    private static async Task<Results<Ok<PayoutDto>, ProblemHttpResult>> MarkPaidAsync(
        Guid id, MarkPaidRequest request, FinanceDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var payout = await db.PainterPayouts.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (payout is null)
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);
        }

        if (request.PaidOn > BusinessDays.Today(clock))
        {
            return ApiProblem.Validation(new Dictionary<string, string[]> { ["paidOn"] = [FinanceMessages.Get("paid_on_future")] });
        }

        if (payout.Status == PayoutStatus.Paid)
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.InvalidTransition);
        }

        // The day of the transfer, at noon in Lisbon, so it falls in that month whatever the time zone.
        var noon = request.PaidOn.ToDateTime(new TimeOnly(12, 0));
        payout.PaidAt = new DateTimeOffset(noon, BusinessDays.Lisbon.GetUtcOffset(noon)).ToUniversalTime();
        payout.Reference = request.Reference.Trim();
        payout.Status = PayoutStatus.Paid;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.InvalidTransition);
        }

        return TypedResults.Ok(ToDto(payout));
    }

    internal static PayoutDto ToDto(PainterPayout p) =>
        new(p.Id, p.PainterId, p.JobId, p.JobCode, Money.ToCents(p.Amount), p.Status, p.CreatedAt, p.PaidAt, p.Reference);
}

/// <summary>A5.4: a closed job done by a painter leaves a pending payout for the pay of the accepted offer, once.</summary>
internal sealed class CreatePayoutOnClose(FinanceDbContext db, TimeProvider clock, ILogger<CreatePayoutOnClose> logger)
    : IIntegrationEventHandler<JobClosed>
{
    public async Task HandleAsync(JobClosed closed, CancellationToken cancellationToken)
    {
        if (closed.PainterId is not { } painterId || await db.PainterPayouts.AnyAsync(p => p.JobId == closed.JobId, cancellationToken))
        {
            return;
        }

        db.PainterPayouts.Add(new PainterPayout
        {
            PainterId = painterId,
            JobId = closed.JobId,
            JobCode = closed.Code,
            Amount = Money.FromCents(closed.PainterPayCents),
            Status = PayoutStatus.Pending,
            CreatedAt = clock.GetUtcNow(),
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (closed.JobId != Guid.Empty)
        {
            // The same event handled twice at once: the unique job index kept one payout.
            db.ChangeTracker.Clear();
            if (!await db.PainterPayouts.AsNoTracking().AnyAsync(p => p.JobId == closed.JobId, cancellationToken))
            {
                throw;
            }

            logger.LogInformation("Payout for job {JobId} already exists", closed.JobId);
        }
    }
}
