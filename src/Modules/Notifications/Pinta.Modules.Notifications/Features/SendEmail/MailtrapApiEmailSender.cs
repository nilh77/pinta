using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MimeKit;
using Pinta.Modules.Notifications.Features.Outbox;

namespace Pinta.Modules.Notifications.Features.SendEmail;

/// <summary>
/// <c>Email:MailtrapApi:*</c>: the Mailtrap Email Sandbox inbox that receives the Demo environment's emails
/// (docs/integrations.md). Railway blocks outgoing SMTP, so they go over HTTPS.
/// </summary>
public sealed class MailtrapApiOptions
{
    public const string ProviderName = "MailtrapApi";

    public string? Token { get; set; }
    public long InboxId { get; set; }
    public Uri BaseUrl { get; set; } = new("https://sandbox.api.mailtrap.io");
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public static bool IsSelected(IConfiguration configuration) =>
        string.Equals(configuration["Email:Provider"], ProviderName, StringComparison.OrdinalIgnoreCase);
}

/// <summary>POST /api/send/{inbox}; anything but 2xx throws, so the outbox records it and Hangfire retries.</summary>
public sealed class MailtrapApiEmailSender(HttpClient http, IOptions<MailtrapApiOptions> options, IOptions<SmtpOptions> email) : IMailTransport
{
    public async Task SendAsync(string to, string subject, string html, CancellationToken cancellationToken)
    {
        var mailtrap = options.Value;
        var from = MailboxAddress.Parse(email.Value.From);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(mailtrap.BaseUrl, $"/api/send/{mailtrap.InboxId}"))
        {
            Content = JsonContent.Create(new
            {
                from = new { email = from.Address, name = from.Name },
                to = new[] { new { email = to } },
                subject,
                html,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mailtrap.Token);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // The body is Mailtrap's error message; it never echoes the token.
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Mailtrap answered {(int)response.StatusCode}: {(body.Length > 300 ? body[..300] : body)}", null, response.StatusCode);
        }
    }
}
