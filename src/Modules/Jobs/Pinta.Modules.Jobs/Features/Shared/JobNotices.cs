using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Domain;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Painters.Contracts;

namespace Pinta.Modules.Jobs.Features.Shared;

/// <summary>
/// Turns what was just saved in the Jobs module into notices (docs/integrations.md, «Avisos y notificaciones push»):
/// every state change, offer, answer, reminder and incident, whichever endpoint or job saved it. Activity that saves
/// nothing here (progress photos, a client's question) calls <see cref="NotifyAsync"/> directly.
/// </summary>
public sealed class JobNotices(INotifier notifier, IUserDirectory users, IPainterDirectory painters, ILogger<JobNotices> logger) : SaveChangesInterceptor
{
    private static readonly AsyncLocal<bool> MutedFlag = new();

    private List<Func<JobsDbContext, Task>> _pending = [];

    /// <summary>No notices while this scope lasts (the demo seed builds its history without waking anyone).</summary>
    public static IDisposable Mute()
    {
        MutedFlag.Value = true;
        return new Unmute();
    }

    private sealed class Unmute : IDisposable
    {
        public void Dispose() => MutedFlag.Value = false;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (!MutedFlag.Value && eventData.Context is JobsDbContext db)
        {
            _pending = Collect(db);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        var pending = _pending;
        _pending = [];
        if (eventData.Context is JobsDbContext db)
        {
            foreach (var send in pending)
            {
                try
                {
                    await send(db);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogError(e, "Could not raise a job notice");
                }
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending = [];
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    /// <summary>A notice for something that saves nothing in this module: progress photos, a client's question.</summary>
    public async Task NotifyAsync(Job job, NotificationKind kind, JobsDbContext db, CancellationToken cancellationToken)
    {
        switch (kind)
        {
            case NotificationKind.ProgressPhotos:
                await notifier.NotifyAsync([job.ClientId], new Notice(kind, NoticeAudience.Client, job.Id, job.Code), cancellationToken);
                await notifier.NotifyAdminsAsync(Admin(kind, job, await PainterNameAsync(db, job.Id, cancellationToken)), cancellationToken);
                break;
            case NotificationKind.ClientQuestion:
                await notifier.NotifyAdminsAsync(Admin(kind, job, await ClientNameAsync(job, cancellationToken)), cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only notices without a saved change are raised by hand.");
        }
    }

    private List<Func<JobsDbContext, Task>> Collect(JobsDbContext db)
    {
        var work = new List<Func<JobsDbContext, Task>>();
        var jobs = db.ChangeTracker.Entries<Job>().ToDictionary(e => e.Entity.Id, e => e.Entity);

        foreach (var change in db.ChangeTracker.Entries<JobStatusChange>().Where(e => e.State == EntityState.Added).Select(e => e.Entity))
        {
            if (jobs.TryGetValue(change.JobId, out var job))
            {
                work.Add(context => OnTransitionAsync(context, job, change));
            }
        }

        foreach (var entry in db.ChangeTracker.Entries<Assignment>().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var assignment = entry.Entity;
            var statusChanged = entry.State == EntityState.Added || entry.Property(a => a.Status).IsModified;
            var reminded = entry.State == EntityState.Modified && entry.Property(a => a.ReminderSentAt).IsModified && assignment.ReminderSentAt is not null;
            var kind = assignment.Status switch
            {
                AssignmentStatus.Offered when entry.State == EntityState.Added => NotificationKind.OfferReceived,
                AssignmentStatus.Declined when statusChanged => NotificationKind.OfferDeclined,
                AssignmentStatus.Withdrawn when statusChanged => NotificationKind.OfferWithdrawn,
                AssignmentStatus.Offered when reminded => NotificationKind.OfferUnanswered,
                _ => (NotificationKind?)null,
            };
            if (kind is { } k)
            {
                work.Add(context => OnAssignmentAsync(context, assignment, k));
            }
        }

        foreach (var incident in db.ChangeTracker.Entries<JobIncident>().Where(e => e.State == EntityState.Added).Select(e => e.Entity))
        {
            work.Add(async context =>
            {
                var job = await context.Jobs.AsNoTracking().SingleAsync(j => j.Id == incident.JobId);
                var painter = await painters.FindAsync(incident.PainterId);
                await notifier.NotifyAdminsAsync(Admin(NotificationKind.IncidentReported, job, painter?.Name));
            });
        }

        foreach (var entry in db.ChangeTracker.Entries<Job>().Where(e => e.State == EntityState.Modified))
        {
            var job = entry.Entity;
            if (entry.Property(j => j.ExpiryReminderFor).IsModified && job.ExpiryReminderFor is not null)
            {
                work.Add(_ => notifier.NotifyAsync([job.ClientId], new Notice(NotificationKind.QuoteExpiring, NoticeAudience.Client, job.Id, job.Code)));
            }
        }

        return work;
    }

    private async Task OnTransitionAsync(JobsDbContext db, Job job, JobStatusChange change)
    {
        Task Client(NotificationKind kind, string? name = null) =>
            notifier.NotifyAsync([job.ClientId], new Notice(kind, NoticeAudience.Client, job.Id, job.Code, Named(name)));

        switch (change.To)
        {
            case JobStatus.InReview when change.From == JobStatus.SiteVisit:
                await notifier.NotifyAdminsAsync(Admin(NotificationKind.SiteVisitReported, job, await UserNameAsync(change.UserId)));
                break;
            case JobStatus.InReview:
                await notifier.NotifyAdminsAsync(Admin(NotificationKind.RequestReceived, job, await ClientNameAsync(job)));
                break;
            case JobStatus.Quoted:
                await Client(NotificationKind.QuoteConfirmed);
                break;
            case JobStatus.Rejected:
                await Client(NotificationKind.RequestRejected);
                break;
            case JobStatus.SiteVisit:
                await Client(NotificationKind.SiteVisitScheduled);
                if (job.SiteVisitAssigneeId is { } assignee)
                {
                    var isPainter = await painters.FindByUserAsync(assignee) is not null;
                    await notifier.NotifyAsync([assignee], new Notice(
                        NotificationKind.SiteVisitAssigned, isPainter ? NoticeAudience.Painter : NoticeAudience.Admin, job.Id, job.Code));
                }

                break;
            case JobStatus.Expired:
                await Client(NotificationKind.QuoteExpired);
                break;
            // The client's «payment received» comes from the payment webhook, for every kind of payment.
            case JobStatus.DepositPaid:
                await notifier.NotifyAdminsAsync(Admin(NotificationKind.DepositPaid, job, await ClientNameAsync(job)));
                break;
            case JobStatus.Scheduled:
            {
                var painter = await PainterNameAsync(db, job.Id);
                await Client(NotificationKind.JobScheduled, painter);
                if (change.Actor == JobActor.Painter)
                {
                    await notifier.NotifyAdminsAsync(Admin(NotificationKind.OfferAccepted, job, painter));
                }

                break;
            }

            case JobStatus.InProgress:
                await notifier.NotifyAdminsAsync(Admin(NotificationKind.JobStarted, job, await PainterNameAsync(db, job.Id)));
                break;
            case JobStatus.Finished:
                await notifier.NotifyAdminsAsync(Admin(NotificationKind.JobFinished, job, await PainterNameAsync(db, job.Id)));
                await Client(NotificationKind.BalanceDue);
                break;
            case JobStatus.Closed:
                await notifier.NotifyAdminsAsync(Admin(NotificationKind.BalancePaid, job));
                break;
            case JobStatus.Cancelled:
                if (change.Actor == JobActor.Client)
                {
                    var kind = change.From == JobStatus.Quoted ? NotificationKind.QuoteRejected : NotificationKind.JobCancelled;
                    await notifier.NotifyAdminsAsync(Admin(kind, job, await ClientNameAsync(job)));
                }

                if (await AssignedPainterAsync(db, job.Id) is { } assigned)
                {
                    await notifier.NotifyAsync([assigned.UserId], new Notice(NotificationKind.JobCancelled, NoticeAudience.Painter, job.Id, job.Code));
                }

                break;
        }
    }

    private async Task OnAssignmentAsync(JobsDbContext db, Assignment assignment, NotificationKind kind)
    {
        var job = await db.Jobs.AsNoTracking().SingleAsync(j => j.Id == assignment.JobId);
        var painter = await painters.FindAsync(assignment.PainterId);
        if (painter is null)
        {
            return;
        }

        if (kind is NotificationKind.OfferReceived or NotificationKind.OfferWithdrawn)
        {
            await notifier.NotifyAsync([painter.UserId], new Notice(kind, NoticeAudience.Painter, job.Id, job.Code));
            return;
        }

        var data = new Dictionary<string, string> { ["name"] = painter.Name };
        if (kind == NotificationKind.OfferDeclined && assignment.DeclineReason is { } reason)
        {
            data["reason"] = reason;
        }

        await notifier.NotifyAdminsAsync(new Notice(kind, NoticeAudience.Admin, job.Id, job.Code, data));
    }

    private static Notice Admin(NotificationKind kind, Job job, string? name = null) => new(kind, NoticeAudience.Admin, job.Id, job.Code, Named(name));

    private static Dictionary<string, string>? Named(string? name) => name is null ? null : new() { ["name"] = name };

    private async Task<string?> ClientNameAsync(Job job, CancellationToken cancellationToken = default) =>
        (await users.FindAsync(job.ClientId, cancellationToken))?.Name;

    private async Task<string?> UserNameAsync(Guid? userId) => userId is { } id ? (await users.FindAsync(id))?.Name : null;

    private async Task<PainterSummary?> AssignedPainterAsync(JobsDbContext db, Guid jobId, CancellationToken cancellationToken = default)
    {
        var painterId = await db.Assignments.AsNoTracking()
            .Where(a => a.JobId == jobId && (a.Status == AssignmentStatus.Accepted || a.Status == AssignmentStatus.Completed))
            .OrderByDescending(a => a.OfferedAt)
            .Select(a => (Guid?)a.PainterId)
            .FirstOrDefaultAsync(cancellationToken);
        return painterId is { } id ? await painters.FindAsync(id, cancellationToken) : null;
    }

    private async Task<string?> PainterNameAsync(JobsDbContext db, Guid jobId, CancellationToken cancellationToken = default) =>
        (await AssignedPainterAsync(db, jobId, cancellationToken))?.Name;
}
