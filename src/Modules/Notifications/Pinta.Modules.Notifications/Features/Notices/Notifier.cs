using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Notifications.Data;
using Pinta.Modules.Notifications.Domain;
using Pinta.Modules.Notifications.Features.Push;

namespace Pinta.Modules.Notifications.Features.Notices;

/// <summary>
/// Stores each notice for the bell right away and sends the push from Hangfire, one job per notice with retries
/// (docs/integrations.md, «Avisos y notificaciones push»). A failure is logged, never thrown at the caller.
/// </summary>
internal sealed class Notifier(
    NotificationsDbContext db, IUserDirectory users, IBackgroundJobClient jobs, PushOptions push, TimeProvider clock, ILogger<Notifier> logger) : INotifier
{
    public async Task NotifyAsync(IReadOnlyCollection<Guid> userIds, Notice notice, CancellationToken cancellationToken = default)
    {
        var recipients = userIds.Distinct().ToList();
        if (recipients.Count == 0)
        {
            return;
        }

        try
        {
            var now = clock.GetUtcNow();
            var created = recipients.Select(userId => new Notification
            {
                UserId = userId,
                Kind = notice.Kind,
                Audience = notice.Audience,
                JobId = notice.JobId,
                JobCode = notice.JobCode,
                Data = notice.Data?.ToDictionary() ?? [],
                CreatedAt = now,
            }).ToList();
            db.Notifications.AddRange(created);
            await db.SaveChangesAsync(cancellationToken);

            if (push.Enabled)
            {
                // Only users with a device signed up get a push job: no empty jobs crowding Hangfire's queue.
                var subscribed = await db.PushSubscriptions.Where(s => recipients.Contains(s.UserId)).Select(s => s.UserId).Distinct()
                    .ToListAsync(cancellationToken);
                foreach (var notification in created.Where(n => subscribed.Contains(n.UserId)))
                {
                    jobs.Enqueue<PushSender>(sender => sender.SendAsync(notification.Id));
                }
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // A notice is a courtesy: the operation that raised it has already happened.
            db.ChangeTracker.Clear();
            logger.LogError(e, "Could not record the {Kind} notice", notice.Kind);
        }
    }

    public async Task NotifyAdminsAsync(Notice notice, CancellationToken cancellationToken = default)
    {
        var admins = await users.ListAdminsAsync(cancellationToken);
        await NotifyAsync([.. admins.Select(a => a.Id)], notice with { Audience = NoticeAudience.Admin }, cancellationToken);
    }
}
