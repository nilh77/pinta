using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pinta.Api.IntegrationTests;

/// <summary>
/// The Demo environment on Railway (docs/deploy-railway.md): migrations, seed and «Restablecer datos de demo» as in
/// Development, but Secure cookies behind the web app's proxy. Own database and storage, because it resets the demo.
/// </summary>
public class DemoEnvironmentTests
{
    [Fact]
    public async Task H8_the_Demo_environment_seeds_the_demo_and_allows_the_reset_over_https()
    {
        await using var demo = new PintaApiFactory { EnvironmentName = "Demo" };
        await demo.InitializeAsync();
        // Secure cookies only travel over HTTPS, as they do from the browser to Railway.
        var admin = demo.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        await admin.RefreshAntiforgeryAsync();

        Assert.Equal(HttpStatusCode.OK, (await admin.LoginAsync(TestData.AdminEmail, TestData.DemoPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync("/api/admin/demo/reset", new { }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task The_API_trusts_the_https_scheme_forwarded_by_the_web_app()
    {
        await using var demo = new PintaApiFactory { EnvironmentName = "Demo" };
        await demo.InitializeAsync();
        // The web app reaches the API over plain HTTP on the private network and passes on the edge's X-Forwarded-Proto.
        var plain = demo.CreateClient();
        var forwarded = demo.CreateClient();
        forwarded.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        Assert.Equal(HttpStatusCode.InternalServerError, (await plain.GetAsync("/api/auth/antiforgery")).StatusCode);
        var response = await forwarded.GetAsync("/api/auth/antiforgery");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.Contains("secure", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.OK, (await plain.GetAsync("/alive")).StatusCode);
    }
}
