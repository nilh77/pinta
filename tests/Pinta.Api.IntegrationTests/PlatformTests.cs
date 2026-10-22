using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Pinta.Api.IntegrationTests;

public class PlatformTests(PintaApiFactory factory)
{
    [Fact]
    public async Task Mutation_without_antiforgery_token_returns_403_antiforgery_failed()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "nathan@demo.pinta.pt", password = TestData.DemoPassword }, ApiClient.Json);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Forbidden, "antiforgery_failed");
    }

    [Theory]
    [InlineData("pt", "Sessão necessária")]
    [InlineData("es", "Necesitas iniciar sesión")]
    [InlineData("en", "Sign-in required")]
    public async Task Error_titles_follow_accept_language(string language, string title)
    {
        var client = await ApiClient.CreateAsync(factory, language);

        var problem = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();

        Assert.Equal(title, problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Unknown_route_returns_404_not_found()
    {
        var response = await factory.CreateClient().GetAsync("/api/does-not-exist");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Hangfire_dashboard_is_only_for_admins()
    {
        var anonymous = await ApiClient.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/hangfire")).StatusCode);

        var client = await ApiClient.CreateAsync(factory);
        await client.RegisterAsync(TestData.UniqueEmail());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/hangfire")).StatusCode);

        var admin = await ApiClient.CreateAsync(factory);
        await admin.LoginAsync("nathan@demo.pinta.pt", TestData.DemoPassword);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/hangfire")).StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Every_module_has_its_own_schema()
    {
        var dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
        await using var command = dataSource.CreateCommand("select schema_name from information_schema.schemata");
        var schemas = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                schemas.Add(reader.GetString(0));
            }
        }

        string[] expected = ["identity", "catalog", "quoting", "jobs", "painters", "payments", "media", "notifications", "finance", "hangfire"];
        Assert.All(expected, schema => Assert.Contains(schema, schemas));
    }

    [Fact]
    public async Task Enums_are_snake_case_strings_in_json()
    {
        var admin = await ApiClient.CreateAsync(factory);
        await admin.LoginAsync("nathan@demo.pinta.pt", TestData.DemoPassword);

        var me = await admin.GetStringAsync("/api/auth/me");

        Assert.Contains("\"role\":\"admin\"", me);
        Assert.Contains("\"locale\":\"pt\"", me);
    }
}
