using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pinta.Api.IntegrationTests;

public class GoogleLoginTests(PintaApiFactory factory)
{
    [Fact]
    public async Task C4_3_google_is_hidden_without_credentials()
    {
        var client = await ApiClient.CreateAsync(factory);

        var providers = await (await client.GetAsync("/api/auth/providers")).ReadJsonAsync();

        Assert.False(providers.GetProperty("google").GetBoolean());
        await ProblemAssert.HasCodeAsync(await client.GetAsync("/api/auth/google?returnUrl=/pt/cotizar/cuenta"), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task C4_3_with_credentials_the_login_goes_to_google_and_comes_back_under_api()
    {
        await using var configured = factory.WithWebHostBuilder(b => b
            .UseSetting("Authentication:Google:ClientId", "test-client-id")
            .UseSetting("Authentication:Google:ClientSecret", "test-secret"));
        var client = configured.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var providers = await (await client.GetAsync("/api/auth/providers")).ReadJsonAsync();
        var challenge = await client.GetAsync("/api/auth/google?returnUrl=/pt/cotizar/cuenta");

        Assert.True(providers.GetProperty("google").GetBoolean());
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var location = challenge.Headers.Location!;
        Assert.Equal("accounts.google.com", location.Host);
        Assert.Contains(Uri.EscapeDataString("/api/auth/google/signin"), location.Query);
    }
}
