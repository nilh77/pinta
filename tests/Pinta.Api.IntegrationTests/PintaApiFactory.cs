using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Pinta.Modules.Notifications.Contracts;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Pinta.Api.IntegrationTests.PintaApiFactory))]

namespace Pinta.Api.IntegrationTests;

/// <summary>The real API on a real PostgreSQL (Testcontainers), in Development so migrations and the demo seed run.</summary>
public sealed class PintaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public InMemoryEmailQueue Emails { get; } = new();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:pinta", _postgres.GetConnectionString());
        builder.UseSetting("RateLimits:AuthPerMinute", "1000");
        builder.UseSetting("Web:BaseUrl", "http://web.test");
        builder.ConfigureTestServices(services => services.AddSingleton<IEmailQueue>(Emails));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
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
