using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Domain;
using Pinta.Modules.Payments.Contracts;
using Pinta.Modules.Quoting.Contracts;

namespace Pinta.Modules.Jobs.Features.Finance;

internal sealed class FinanceJobs(JobsDbContext db, IQuoteVersions quotes, IJobPayments payments) : IFinanceJobs
{
    public async Task<IReadOnlyList<FinanceJob>> InPeriodAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        // The last time each job reached "Cerrado" or "Cancelado" in the period; both are final, so there is one.
        var changes = await db.Set<JobStatusChange>().AsNoTracking()
            .Where(c => (c.To == JobStatus.Closed || c.To == JobStatus.Cancelled) && c.ChangedAt >= from && c.ChangedAt < to)
            .ToListAsync(cancellationToken);
        var jobIds = changes.Select(c => c.JobId).Distinct().ToList();
        var jobs = await db.Jobs.AsNoTracking().Where(j => jobIds.Contains(j.Id)).ToDictionaryAsync(j => j.Id, cancellationToken);

        var result = new List<FinanceJob>();
        foreach (var change in changes.OrderBy(c => c.ChangedAt))
        {
            var job = jobs[change.JobId];
            if (job.Status != change.To)
            {
                continue;
            }

            var quote = job.QuoteValidUntil is null ? null : await quotes.GetAdminAsync(job.CurrentQuoteVersionId, cancellationToken);
            if (quote is null)
            {
                continue;
            }

            var b = quote.Breakdown;
            if (change.To == JobStatus.Closed)
            {
                result.Add(new FinanceJob(job.Id, job.Code, job.Status, change.ChangedAt, b.SubtotalCents, b.VatCents, b.TotalCents, b.MaterialCents,
                    b.PainterTotalCents, 0));
                continue;
            }

            var retained = await payments.DepositRetainedCentsAsync(job.Id, cancellationToken);
            if (retained > 0)
            {
                result.Add(new FinanceJob(job.Id, job.Code, job.Status, change.ChangedAt, b.SubtotalCents, b.VatCents, b.TotalCents, 0, 0, retained));
            }
        }

        return result;
    }
}
