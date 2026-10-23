using Hangfire;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Quoting.Contracts;
using Pinta.SharedKernel.Http;
using Pinta.SharedKernel.Time;

namespace Pinta.Modules.Jobs.Features.Expiry;

/// <summary>Daily at 09:00 Lisbon (S3): the client is reminded once that the confirmed price expires in 3 days.</summary>
public sealed class QuoteExpiryReminders(
    JobsDbContext db, IQuoteVersions quotes, IUserDirectory users, IEmailQueue emails, WebLinks links, TimeProvider clock)
{
    public const string JobId = "quote-expiry-reminders";

    [AutomaticRetry(Attempts = 3)]
    public async Task<int> RunAsync()
    {
        var inThreeDays = BusinessDays.Today(clock).AddDays(3);
        var jobs = await db.Jobs
            .Where(j => j.Status == JobStatus.Quoted && j.QuoteValidUntil == inThreeDays && j.ExpiryReminderFor != inThreeDays)
            .ToListAsync();
        foreach (var job in jobs)
        {
            var client = await users.FindAsync(job.ClientId);
            var quote = await quotes.GetClientAsync(job.CurrentQuoteVersionId, "pt");
            if (client is not null && quote is not null)
            {
                await emails.EnqueueAsync(new PriceExpiringEmail(
                    client.Email, client.Locale, client.Name, job.Code, inThreeDays, quote.DepositCents, links.ClientJob(job.Id, client.Locale)) { JobId = job.Id });
            }

            job.ExpiryReminderFor = inThreeDays;
        }

        await db.SaveChangesAsync();
        return jobs.Count;
    }
}

/// <summary>Daily at 09:00 Lisbon (S3): one email to the admins with the requests past their review deadline.</summary>
public sealed class OverdueReviewAlerts(JobsDbContext db, IUserDirectory users, IEmailQueue emails, WebLinks links, TimeProvider clock)
{
    public const string JobId = "overdue-review-alerts";

    [AutomaticRetry(Attempts = 3)]
    public async Task<int> RunAsync()
    {
        var today = BusinessDays.Today(clock);
        var codes = await db.Jobs.Where(j => j.Status == JobStatus.InReview && j.ReviewDueOn < today).OrderBy(j => j.ReviewDueOn)
            .Select(j => j.Code).ToListAsync();
        if (codes.Count > 0)
        {
            foreach (var admin in await users.ListAdminsAsync())
            {
                await emails.EnqueueAsync(new OverdueReviewsEmail(admin.Email, admin.Locale, codes, links.AdminReviews(admin.Locale)));
            }
        }

        return codes.Count;
    }
}
