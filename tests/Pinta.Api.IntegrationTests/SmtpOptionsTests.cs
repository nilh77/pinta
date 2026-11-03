using Microsoft.Extensions.Configuration;
using Pinta.Modules.Notifications.Features.SendEmail;

namespace Pinta.Api.IntegrationTests;

/// <summary>The SMTP connection string: Mailpit locally, a test inbox with a login in the Demo environment.</summary>
public class SmtpOptionsTests
{
    [Fact]
    public void Mailpit_from_Aspire_has_no_login()
    {
        var options = Configure("Endpoint=smtp://localhost:61025");

        Assert.Equal(("localhost", 61025, (string?)null), (options.Host, options.Port, options.Username));
    }

    [Fact]
    public void A_test_inbox_carries_its_login()
    {
        var options = Configure("Endpoint=smtp://sandbox.smtp.mailtrap.io:2525;Username=inbox-user;Password=inbox-pass");

        Assert.Equal(("sandbox.smtp.mailtrap.io", 2525, "inbox-user", "inbox-pass"), (options.Host, options.Port, options.Username, options.Password));
    }

    private static SmtpOptions Configure(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:mailpit"] = connectionString })
            .Build();
        var options = new SmtpOptions();
        options.Configure(configuration);
        return options;
    }
}
