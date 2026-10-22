using Hangfire;
using Pinta.Modules.Notifications.Contracts;

namespace Pinta.Modules.Notifications.Features.SendEmail;

/// <summary>Renders the email now (so the job only carries strings) and sends it from a Hangfire job.</summary>
internal sealed class HangfireEmailQueue(EmailRenderer renderer, IBackgroundJobClient jobs) : IEmailQueue
{
    public async Task EnqueueAsync(Email email, CancellationToken cancellationToken = default)
    {
        var (subject, html) = await renderer.RenderAsync(email);
        jobs.Enqueue<SmtpEmailSender>(sender => sender.SendAsync(email.To, subject, html, CancellationToken.None));
    }
}
