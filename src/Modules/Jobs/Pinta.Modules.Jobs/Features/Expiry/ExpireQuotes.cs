using Hangfire;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Domain;
using Pinta.Modules.Jobs.Features.Shared;
using Pinta.Modules.Notifications.Contracts;
using Pinta.SharedKernel.Time;

using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Jobs.Features.Expiry;

/// <summary>
/// Daily at 03:00 Lisbon (S3): confirmed prices past their last valid day move to "Expirado" and the client gets an
/// email. Confirmed on day D, valid through D+15, expired on D+16 (docs/pricing-engine.md).
/// </summary>
public sealed class ExpireQuotes(JobsDbContext db, IUserDirectory users, IEmailQueue emails, WebLinks links, TimeProvider clock)
{
    public const string JobId = "expire-quotes";

    [AutomaticRetry(Attempts = 3)]
    public async Task<int> RunAsync()
    {
        var today = BusinessDays.Today(clock);
        var jobs = await db.Jobs.Where(j => j.Status == JobStatus.Quoted && j.QuoteValidUntil < today).ToListAsync();
        foreach (var job in jobs)
        {
            job.TransitionTo(JobStatus.Expired, JobActor.System, null, null, clock.GetUtcNow());
            await db.SaveChangesAsync();
            var client = await users.FindAsync(job.ClientId);
            if (client is not null)
            {
                await emails.EnqueueAsync(new PriceExpiredEmail(client.Email, client.Locale, client.Name, job.Code, links.ClientJob(job.Id, client.Locale))
                {
                    JobId = job.Id,
                });
            }
        }

        return jobs.Count;
    }
}
