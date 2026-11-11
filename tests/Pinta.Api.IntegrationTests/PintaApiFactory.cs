using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Payments.Stripe;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Pinta.Api.IntegrationTests.PintaApiFactory))]

namespace Pinta.Api.IntegrationTests;

/// <summary>
/// The real API on a real PostgreSQL (Testcontainers), in Development so migrations and the demo seed run, or in another
/// environment for the tests that check what changes there (the Demo environment on Railway).
/// </summary>
public sealed class PintaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    // Same image as the AppHost: MinIO no longer publishes images on Docker Hub.
    private readonly MinioContainer _minio = new MinioBuilder("cgr.dev/chainguard/minio:latest").Build();

    public string EnvironmentName { get; init; } = "Development";

    public InMemoryEmailQueue Emails { get; } = new();

    public FakeStripe Stripe { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _minio.StartAsync());
        // Build the host once, before tests run in parallel: the lazy first CreateClient is not thread-safe and two
        // hosts would migrate and seed the same database at the same time.
        _ = Server;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        // appsettings.Development.json is read only in Development; on Railway it is the Seed__DemoPassword variable.
        builder.UseSetting("Seed:DemoPassword", TestData.DemoPassword);
        builder.UseSetting("ConnectionStrings:pinta", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:minio", $"Endpoint={_minio.GetConnectionString()};AccessKey={_minio.GetAccessKey()};SecretKey={_minio.GetSecretKey()}");
        builder.UseSetting("RateLimits:AuthPerMinute", "1000");
        builder.UseSetting("RateLimits:EstimatePerMinute", "1000");
        builder.UseSetting("RateLimits:UploadsPerMinute", "1000");
        builder.UseSetting("Web:BaseUrl", "http://web.test");
        builder.UseSetting("Stripe:WebhookSecret", FakeStripe.WebhookSecret);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IEmailQueue>(Emails);
            services.AddSingleton<IStripeGateway>(Stripe);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _minio.DisposeAsync();
    }
}

/// <summary>Captures queued emails instead of sending them through Hangfire and Mailpit.</summary>
public sealed class InMemoryEmailQueue : IEmailQueue
{
    private readonly ConcurrentQueue<Email> _emails = new();

    public Task EnqueueAsync(Email email, CancellationToken cancellationToken = default)
    {
        _emails.Enqueue(email);
        return Task.CompletedTask;
    }

    public IReadOnlyList<T> To<T>(string address) where T : Email =>
        _emails.OfType<T>().Where(e => e.To == address).ToList();
}
