using System.Net;
using System.Net.Http.Json;

namespace Pinta.Api.IntegrationTests;

public class CoverageTests(PintaApiFactory factory)
{
    [Theory]
    [InlineData("2710-123", true, "Sintra")]
    [InlineData("1000-001", true, "Lisboa")]
    [InlineData("4000-123", false, null)]
    public async Task C4_1_checks_the_postal_code_against_the_service_area(string postalCode, bool covered, string? municipality)
    {
        var client = await ApiClient.CreateAsync(factory);

        var result = await (await client.GetAsync($"/api/coverage/check?postalCode={postalCode}")).ReadJsonAsync();

        Assert.Equal(covered, result.GetProperty("covered").GetBoolean());
        Assert.Equal(municipality, result.GetProperty("municipality").GetString());
    }

    [Fact]
    public async Task C4_1_rejects_a_malformed_postal_code()
    {
        var client = await ApiClient.CreateAsync(factory);

        var response = await client.GetAsync("/api/coverage/check?postalCode=2710123");

        var problem = await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("postalCode", out _));
    }

    [Fact]
    public async Task C4_1_outside_the_area_the_visitor_can_join_the_waitlist_once()
    {
        var client = await ApiClient.CreateAsync(factory);
        var body = new { email = TestData.UniqueEmail(), postalCode = "4000-123", locale = "es" };

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/coverage/waitlist", body, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/coverage/waitlist", body, ApiClient.Json)).StatusCode);

        var invalid = await client.PostAsJsonAsync("/api/coverage/waitlist", new { email = "nope", postalCode = "4000", locale = "es" }, ApiClient.Json);
        var problem = await ProblemAssert.HasCodeAsync(invalid, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("email", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("postalCode", out _));
    }

    [Fact]
    public async Task C4_2_public_catalog_exposes_the_minimum_lead_time()
    {
        var client = await ApiClient.CreateAsync(factory);

        var catalog = await (await client.GetAsync("/api/catalog")).ReadJsonAsync();

        Assert.Equal(3, catalog.GetProperty("minLeadBusinessDays").GetInt32());
    }
}

public class WaitlistAdminTests(PintaApiFactory factory)
{
    [Fact]
    public async Task A1_7_admins_list_and_export_the_waitlist()
    {
        var email = $"=wait-{Guid.NewGuid():N}@test.pt";
        var visitor = await ApiClient.CreateAsync(factory);
        await visitor.PostAsJsonAsync("/api/coverage/waitlist", new { email, postalCode = "4000-123", locale = "es" }, ApiClient.Json);
        var admin = await ApiClient.CreateAdminAsync(factory);

        var list = await (await admin.GetAsync("/api/admin/coverage/waitlist")).ReadJsonAsync();
        var csv = await (await admin.GetAsync("/api/admin/coverage/waitlist/export")).Content.ReadAsStringAsync();

        Assert.Contains(list.EnumerateArray(), w => w.GetProperty("email").GetString() == email && w.GetProperty("locale").GetString() == "es");
        Assert.Contains($"'{email},4000-123,es,", csv);
        await ProblemAssert.HasCodeAsync(await visitor.GetAsync("/api/admin/coverage/waitlist"), System.Net.HttpStatusCode.Unauthorized, "unauthenticated");
    }
}
