using Hangfire;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Notifications.Data;
using Pinta.Modules.Notifications.Domain;
using Pinta.Modules.Notifications.Features.Outbox;

namespace Pinta.Modules.Notifications.Features.SendEmail;

/// <summary>Renders the email now, records it in the outbox and sends it from a Hangfire job (S2).</summary>
public sealed class HangfireEmailQueue(EmailRenderer renderer, NotificationsDbContext db, IBackgroundJobClient jobs, TimeProvider clock) : IEmailQueue
{
    public async Task EnqueueAsync(Email email, CancellationToken cancellationToken = default)
    {
        var (subject, html) = await renderer.RenderAsync(email);
        var outgoing = new OutgoingEmail
        {
            To = email.To,
            Type = email.GetType().Name[..^"Email".Length],
            Subject = subject,
            Html = html,
            JobId = email.JobId,
            CreatedAt = clock.GetUtcNow(),
        };
        db.Outbox.Add(outgoing);
        await db.SaveChangesAsync(cancellationToken);
        jobs.Enqueue<OutboxSender>(sender => sender.SendAsync(outgoing.Id));
    }
}
