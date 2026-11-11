using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Pinta.Modules.Notifications.Features.Outbox;

namespace Pinta.Modules.Notifications.Features.SendEmail;

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IMailTransport
{
    public async Task SendAsync(string to, string subject, string html, CancellationToken cancellationToken)
    {
        var smtp = options.Value;
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(smtp.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

        using var client = new SmtpClient();
        // Mailpit offers no STARTTLS and needs no login; the test inbox of the Demo environment wants both.
        await client.ConnectAsync(smtp.Host, smtp.Port, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
        if (!string.IsNullOrEmpty(smtp.Username))
        {
            await client.AuthenticateAsync(smtp.Username, smtp.Password ?? "", cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
