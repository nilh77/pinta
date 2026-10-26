using Hangfire;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Notifications.Data;
using Pinta.Modules.Notifications.Domain;

namespace Pinta.Modules.Notifications.Features.Outbox;

/// <summary>Sends what transports an email (SMTP to Mailpit locally).</summary>
public interface IMailTransport
{
    Task SendAsync(string to, string subject, string html, CancellationToken cancellationToken);
}

/// <summary>Hangfire job: sends one outbox email, recording every attempt; Hangfire retries a failure up to 5 times.</summary>
public sealed class OutboxSender(NotificationsDbContext db, IMailTransport transport, TimeProvider clock)
{
    [AutomaticRetry(Attempts = 5)]
    public async Task SendAsync(Guid outgoingEmailId)
    {
        var email = await db.Outbox.SingleOrDefaultAsync(e => e.Id == outgoingEmailId);
        if (email is null || email.Status == OutgoingEmailStatus.Sent)
        {
            return;
        }

        email.Attempts++;
        try
        {
            await transport.SendAsync(email.To, email.Subject, email.Html, CancellationToken.None);
            email.Status = OutgoingEmailStatus.Sent;
            email.SentAt = clock.GetUtcNow();
            email.LastError = null;
        }
        catch (Exception e)
        {
            email.Status = OutgoingEmailStatus.Failed;
            email.LastError = e.Message.Length > 1000 ? e.Message[..1000] : e.Message;
            await db.SaveChangesAsync();
            throw;
        }

        await db.SaveChangesAsync();
    }
}

internal sealed class EmailLog(NotificationsDbContext db) : IEmailLog
{
    public async Task<IReadOnlyList<SentEmail>> ListForJobAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        await db.Outbox.AsNoTracking()
            .Where(e => e.JobId == jobId)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new SentEmail(e.Type, e.To, e.Subject, e.Status.ToString().ToLower(), e.Attempts, e.CreatedAt, e.SentAt))
            .ToListAsync(cancellationToken);

    public Task<int> AnonymizeRecipientAsync(string email, string replacement, CancellationToken cancellationToken = default) =>
        db.Outbox
            .Where(e => e.To.ToLower() == email.ToLower() && e.Status != OutgoingEmailStatus.Queued)
            .ExecuteUpdateAsync(e => e.SetProperty(x => x.To, replacement).SetProperty(x => x.Html, ""), cancellationToken);
}
