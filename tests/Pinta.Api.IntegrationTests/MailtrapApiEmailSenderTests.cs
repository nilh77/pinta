using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Pinta.Modules.Notifications;
using Pinta.Modules.Notifications.Features.Outbox;
using Pinta.Modules.Notifications.Features.SendEmail;

namespace Pinta.Api.IntegrationTests;

/// <summary>Emails of the Demo environment over Mailtrap's sandbox API, because Railway blocks outgoing SMTP.</summary>
public class MailtrapApiEmailSenderTests
{
    [Fact]
    public async Task An_email_goes_to_the_sandbox_inbox_with_the_token()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var sender = Sender(handler);

        await sender.SendAsync("ana@demo.pinta.pt", "Recebemos o seu pedido", "<p>Olá</p>", CancellationToken.None);

        Assert.Equal("https://sandbox.api.mailtrap.io/api/send/123", handler.Request!.RequestUri!.ToString());
        Assert.Equal(("Bearer", "test-token"), (handler.Request.Headers.Authorization!.Scheme, handler.Request.Headers.Authorization.Parameter));
        var body = JsonDocument.Parse(handler.Body!).RootElement;
        Assert.Equal("no-reply@pinta.pt", body.GetProperty("from").GetProperty("email").GetString());
        Assert.Equal("Pinta", body.GetProperty("from").GetProperty("name").GetString());
        Assert.Equal("ana@demo.pinta.pt", body.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal(("Recebemos o seu pedido", "<p>Olá</p>"), (body.GetProperty("subject").GetString(), body.GetProperty("html").GetString()));
    }

    [Fact]
    public async Task A_rejected_email_throws_so_Hangfire_retries()
    {
        var sender = Sender(new RecordingHandler(HttpStatusCode.Unauthorized, """{"errors":["Unauthorized"]}"""));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => sender.SendAsync("ana@demo.pinta.pt", "s", "h", CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Contains("Unauthorized", error.Message);
        Assert.DoesNotContain("test-token", error.Message);
    }

    [Fact]
    public void The_provider_is_chosen_by_configuration_with_its_timeout()
    {
        Assert.IsType<SmtpEmailSender>(Transport(new Dictionary<string, string?>()));

        var services = Services(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "MailtrapApi",
            ["Email:MailtrapApi:Token"] = "test-token",
            ["Email:MailtrapApi:InboxId"] = "123",
        });
        Assert.IsType<MailtrapApiEmailSender>(services.GetRequiredService<IMailTransport>());
        Assert.Equal(TimeSpan.FromSeconds(30), services.GetRequiredService<IOptions<MailtrapApiOptions>>().Value.Timeout);
    }

    [Fact]
    public void Mailtrap_without_a_token_fails_at_startup()
    {
        var services = Services(new Dictionary<string, string?> { ["Email:Provider"] = "MailtrapApi", ["Email:MailtrapApi:InboxId"] = "123" });

        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<MailtrapApiOptions>>().Value);
    }

    private static MailtrapApiEmailSender Sender(HttpMessageHandler handler) =>
        new(new HttpClient(handler), Options.Create(new MailtrapApiOptions { Token = "test-token", InboxId = 123 }), Options.Create(new SmtpOptions()));

    private static IMailTransport Transport(Dictionary<string, string?> settings) => Services(settings).GetRequiredService<IMailTransport>();

    private static ServiceProvider Services(Dictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.AddNotificationsModule();
        return builder.Services.BuildServiceProvider();
    }

    private sealed class RecordingHandler(HttpStatusCode status, string response = """{"success":true}""") : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(response) };
        }
    }
}
