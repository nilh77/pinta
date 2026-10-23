using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Domain;
using Pinta.Modules.Quoting.Contracts;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;
using Pinta.SharedKernel.Time;

namespace Pinta.Modules.Jobs.Features.Shared;

public sealed record PreferredDatesDto(bool Asap, DateOnly? From, DateOnly? To);

public sealed record CancellationPreview(bool Cancellable, long DepositPaidCents, long RefundCents);

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
    /// <summary>
    /// The refund if cancelled now: the deposit (none until payments arrive in H5) under the policy of
    /// docs/pricing-engine.md; nothing to cancel from "En curso" on.
    /// </summary>
    public static CancellationPreview Preview(Job job, Pinta.Pricing.PricingRules rules, bool fullRefund = false)
    {
        var cancellable = JobStateMachine.CancellableFrom.Contains(job.Status);
        const decimal depositPaid = 0m;
        var refund = fullRefund ? depositPaid : Pinta.Pricing.CancellationPolicy.Refund(rules, depositPaid, null);
        return new CancellationPreview(cancellable, Money.ToCents(depositPaid), cancellable ? Money.ToCents(refund) : 0);
    }

    /// <summary>The draft started after a site visit comes from it; otherwise from the admin's review.</summary>
    public static QuoteOrigin DraftOrigin(Job job) => job.SiteVisitAt is null ? QuoteOrigin.AdminReview : QuoteOrigin.SiteVisit;

    public static async Task<Func<DateOnly>> ReviewDueOnAsync(IRequestSettings settings, TimeProvider clock, CancellationToken cancellationToken)
    {
        var days = (await settings.GetAsync(cancellationToken)).ReviewDeadlineBusinessDays;
        return () => BusinessDays.Add(BusinessDays.Today(clock), days);
    }
}
