using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Contracts;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Domain;
using Pinta.SharedKernel.Events;
using Pinta.SharedKernel.Time;

namespace Pinta.Modules.Jobs.Features.Review;

/// <summary>Confirming the email moves the client's pending requests to review and starts the deadline (C4.5).</summary>
internal sealed class MoveToReviewWhenEmailConfirmed(JobsDbContext db, IRequestSettings settingsProvider, TimeProvider clock)
    : IIntegrationEventHandler<EmailConfirmed>
{
    public async Task HandleAsync(EmailConfirmed integrationEvent, CancellationToken cancellationToken)
    {
        var pending = await db.Jobs
            .Include(j => j.History)
            .Where(j => j.ClientId == integrationEvent.UserId && j.Status == JobStatus.PendingEmail)
            .ToListAsync(cancellationToken);
        if (pending.Count == 0)
        {
            return;
        }

        var settings = await settingsProvider.GetAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var dueOn = BusinessDays.Add(BusinessDays.Today(clock), settings.ReviewDeadlineBusinessDays);
        foreach (var job in pending)
        {
            job.TransitionTo(JobStatus.InReview, JobActor.System, integrationEvent.UserId, null, now, () => dueOn);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
