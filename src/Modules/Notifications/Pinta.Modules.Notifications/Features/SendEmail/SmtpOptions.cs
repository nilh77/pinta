using System.Data.Common;
using Microsoft.Extensions.Configuration;

namespace Pinta.Modules.Notifications.Features.SendEmail;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "Pinta <no-reply@pinta.pt>";

    /// <summary>
    /// Reads <c>Email:From</c> and the connection string <c>mailpit</c>: <c>Endpoint=smtp://host:port</c> from Aspire
    /// locally, plus <c>Username</c> and <c>Password</c> for the SMTP test inbox of the Demo environment (docs/integrations.md).
    /// </summary>
    public void Configure(IConfiguration configuration)
    {
        From = configuration["Email:From"] ?? From;
        var connectionString = configuration.GetConnectionString("mailpit");
        if (string.IsNullOrEmpty(connectionString))
        {
            return;
        }

        var parts = new DbConnectionStringBuilder { ConnectionString = connectionString };
        if (parts.TryGetValue("Endpoint", out var endpoint) && Uri.TryCreate(endpoint.ToString(), UriKind.Absolute, out var uri))
        {
            Host = uri.Host;
            Port = uri.Port;
        }

        Username = parts.TryGetValue("Username", out var username) ? username.ToString() : null;
        Password = parts.TryGetValue("Password", out var password) ? password.ToString() : null;
    }
}
