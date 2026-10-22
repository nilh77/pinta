using Hangfire;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Pinta.Modules.Notifications.Features.SendEmail;

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options)
{
    [AutomaticRetry(Attempts = 5)]
    public async Task SendAsync(string to, string subject, string html, CancellationToken cancellationToken)
    {
        var smtp = options.Value;
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(smtp.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

        using var client = new SmtpClient();
        // shortcut: no TLS or auth because Mailpit is local; add both when a production provider is chosen (Phase 8).
        await client.ConnectAsync(smtp.Host, smtp.Port, SecureSocketOptions.None, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
