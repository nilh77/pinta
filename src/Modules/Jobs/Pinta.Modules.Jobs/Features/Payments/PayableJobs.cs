using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Domain;
using Pinta.Modules.Payments.Contracts;
using Pinta.Modules.Quoting.Contracts;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Events;

namespace Pinta.Modules.Jobs.Features.Payments;

internal sealed class PayableJobs(JobsDbContext db, IQuoteVersions quotes) : IPayableJobs
{
    public async Task<PayableJob?> FindAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await db.Jobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        // Amounts only once a price is confirmed; before that the current version is the client's estimate.
        var quote = job.QuoteValidUntil is null ? null : await quotes.GetClientAsync(job.CurrentQuoteVersionId, "pt", cancellationToken);
        return new PayableJob(
            job.Id, job.Code, job.ClientId, job.Status, job.QuoteValidUntil, quote?.TotalCents, quote?.DepositCents, quote?.BalanceCents,
            Money.ToCents(job.SiteVisitPrice ?? 0), job.TaxId, job.CompanyName, job.AddressLine1, job.PostalCode, job.Locality);
    }
}

/// <summary>
/// A payment confirmed by Stripe moves the job (docs/states.md, actor "Sistema"): the deposit takes the confirmed price to
/// "Señal pagada", the balance takes a finished job to "Cerrado". A site visit payment changes no state.
/// </summary>
internal sealed class MoveJobWhenPaid(JobsDbContext db, TimeProvider clock, ILogger<MoveJobWhenPaid> logger)
    : IIntegrationEventHandler<PaymentSucceeded>
{
    public async Task HandleAsync(PaymentSucceeded paid, CancellationToken cancellationToken)
    {
        var to = paid.Type switch
        {
            PaymentType.Deposit => JobStatus.DepositPaid,
            PaymentType.Balance => JobStatus.Closed,
            _ => (JobStatus?)null,
        };
        var job = to is null ? null : await db.Jobs.SingleOrDefaultAsync(j => j.Id == paid.JobId, cancellationToken);
        if (job is null || job.Status == to)
        {
            return;
        }

        if (!JobStateMachine.IsAllowed(job.Status, to!.Value, JobActor.System))
        {
            logger.LogWarning("Payment {PaymentId} arrived for job {JobId} in {Status}; no transition", paid.PaymentId, job.Id, job.Status);
            return;
        }

        job.TransitionTo(to.Value, JobActor.System, null, null, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }
}
