using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Domain;
using Pinta.Modules.Quoting.Contracts;
using Pinta.Modules.Payments.Contracts;
using Pinta.SharedKernel.Events;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;
using Pinta.SharedKernel.Time;

namespace Pinta.Modules.Jobs.Features.Shared;

public sealed record PreferredDatesDto(bool Asap, DateOnly? From, DateOnly? To);

/// <summary>
/// <see cref="DepositPaidCents"/> is the deposit still held; <see cref="RefundCents"/> what the policy gives back now
/// (all of it with no scheduled date or 7+ days before it, half after; docs/pricing-engine.md).
/// </summary>
public sealed record CancellationPreview(bool Cancellable, long DepositPaidCents, long RefundCents, int? DaysBeforeStart);

internal static class JobProblems
{
    public static ProblemHttpResult NotFound() => ApiProblem.Create(StatusCodes.Status404NotFound, ErrorCodes.NotFound);

    public static ProblemHttpResult InvalidTransition() => ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.InvalidTransition);

    public static ProblemHttpResult ConcurrencyConflict() => ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict);
}

internal static class JobSaving
{
    /// <summary>
    /// Saves a job edited against <paramref name="expectedVersion"/> (the admin's copy); false when someone else changed
    /// it first. Without a version, the row version still protects against concurrent saves.
    /// </summary>
    public static async Task<bool> TrySaveAsync(this JobsDbContext db, Job job, uint? expectedVersion, CancellationToken cancellationToken)
    {
        if (expectedVersion is { } version)
        {
            if (job.Version != version)
            {
                return false;
            }

            db.Entry(job).Property(j => j.Version).OriginalValue = version;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}

internal static class JobRules
{
    /// <summary>A paid site visit comes off the next price when the settings say it is deductible (TC15).</summary>
    public static async Task<long> SiteVisitCreditCentsAsync(
        Job job, Pinta.Modules.Payments.Contracts.IJobPayments payments, IRequestSettings settings, CancellationToken cancellationToken) =>
        job.SiteVisitAt is null || !(await settings.GetAsync(cancellationToken)).SiteVisitDeductible
            ? 0
            : await payments.SiteVisitPaidCentsAsync(job.Id, cancellationToken);

    /// <summary>The draft started after a site visit comes from it; otherwise from the admin's review.</summary>
    public static QuoteOrigin DraftOrigin(Job job) => job.SiteVisitAt is null ? QuoteOrigin.AdminReview : QuoteOrigin.SiteVisit;

    public static async Task<Func<DateOnly>> ReviewDueOnAsync(IRequestSettings settings, TimeProvider clock, CancellationToken cancellationToken)
    {
        var days = (await settings.GetAsync(cancellationToken)).ReviewDeadlineBusinessDays;
        return () => BusinessDays.Add(BusinessDays.Today(clock), days);
    }
}

/// <summary>Cancellation (C5.6, A3.4): the refund under the policy, and telling Payments to give it back.</summary>
internal sealed class JobCancellation(IJobPayments payments, IPriceSnapshotProvider snapshots, IEventPublisher events, TimeProvider clock)
{
    public async Task<CancellationPreview> PreviewAsync(Job job, bool fullRefund, CancellationToken cancellationToken)
    {
        var cancellable = JobStateMachine.CancellableFrom.Contains(job.Status);
        var held = await payments.DepositHeldCentsAsync(job.Id, cancellationToken);
        int? days = job.ScheduledOn is { } start ? start.DayNumber - BusinessDays.Today(clock).DayNumber : null;
        var rules = (await snapshots.GetCurrentAsync(cancellationToken)).Rules;
        var refund = fullRefund ? held : Money.ToCents(Pinta.Pricing.CancellationPolicy.Refund(rules, Money.FromCents(held), days));
        return new CancellationPreview(cancellable, held, cancellable ? refund : 0, days);
    }

    /// <summary>After the job is saved as cancelled: Payments refunds <paramref name="preview"/> and closes open payments.</summary>
    public Task CancelledAsync(Job job, CancellationPreview preview, string? reason, CancellationToken cancellationToken) =>
        events.PublishAsync(new JobCancelled(job.Id, preview.RefundCents, reason), cancellationToken);
}
