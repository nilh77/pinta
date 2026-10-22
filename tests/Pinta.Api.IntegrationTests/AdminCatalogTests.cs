using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pinta.Api.IntegrationTests;

public class AdminCatalogTests(PintaApiFactory factory)
{
    [Fact]
    public async Task A1_admin_catalog_requires_the_admin_role()
    {
        var anonymous = await ApiClient.CreateAsync(factory);
        await ProblemAssert.HasCodeAsync(await anonymous.GetAsync("/api/admin/catalog/paints"), HttpStatusCode.Unauthorized, "unauthenticated");

        var client = await ApiClient.CreateAsync(factory);
        await client.RegisterAsync(TestData.UniqueEmail());
        await ProblemAssert.HasCodeAsync(await client.GetAsync("/api/admin/catalog/paints"), HttpStatusCode.Forbidden, "forbidden");
        await ProblemAssert.HasCodeAsync(await client.GetAsync("/api/admin/settings"), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task A1_1_admin_creates_and_edits_a_paint_and_a_stale_version_conflicts()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var key = UniqueKey("paint");

        var created = await admin.PostAsJsonAsync("/api/admin/catalog/paints", new
        {
            key,
            name = new { pt = "Esmalte", es = "Esmalte", en = (string?)null },
            pricePerLitreCents = 1250,
            coverageM2PerLitre = 12.5m,
            coats = 2,
            isActive = true,
        }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var paint = await created.ReadJsonAsync();
        var id = paint.GetProperty("id").GetString();
        Assert.Equal($"/api/admin/catalog/paints/{id}", created.Headers.Location?.OriginalString);
        Assert.Equal(1250, paint.GetProperty("pricePerLitreCents").GetInt64());
        var version = paint.GetProperty("version").GetUInt32();

        var edit = new
        {
            name = new { pt = "Esmalte aquoso", es = "Esmalte al agua", en = "Water-based enamel" },
            pricePerLitreCents = 1300,
            coverageM2PerLitre = 12.5m,
            coats = 3,
            isActive = false,
            version,
        };
        var updated = await admin.PutAsJsonAsync($"/api/admin/catalog/paints/{id}", edit, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var after = await updated.ReadJsonAsync();
        Assert.Equal(1300, after.GetProperty("pricePerLitreCents").GetInt64());
        Assert.False(after.GetProperty("isActive").GetBoolean());
        Assert.NotEqual(version, after.GetProperty("version").GetUInt32());

        var stale = await admin.PutAsJsonAsync($"/api/admin/catalog/paints/{id}", edit, ApiClient.Json);
        await ProblemAssert.HasCodeAsync(stale, HttpStatusCode.Conflict, "concurrency_conflict");

        var list = await (await admin.GetAsync("/api/admin/catalog/paints")).ReadJsonAsync();
        Assert.Contains(list.EnumerateArray(), p => p.GetProperty("key").GetString() == key);
    }

    [Fact]
    public async Task A1_1_invalid_paint_returns_422_per_field()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/admin/catalog/paints", new
        {
            key = "matte",
            name = new { pt = "" },
            pricePerLitreCents = -1,
            coverageM2PerLitre = 0m,
            coats = 2,
            isActive = true,
        }, ApiClient.Json);

        var problem = await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("name.pt", out _));
        Assert.True(errors.TryGetProperty("pricePerLitreCents", out _));
        Assert.True(errors.TryGetProperty("coverageM2PerLitre", out _));
    }

    [Fact]
    public async Task A1_1_duplicate_key_is_rejected()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/admin/catalog/extras", new
        {
            key = "anti_damp_treatment",
            name = new { pt = "Outro" },
            clientPriceCents = 100,
            painterPayCents = 50,
            isActive = true,
        }, ApiClient.Json);

        var problem = await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("key", out _));
    }

    [Fact]
    public async Task A1_1_suggested_paint_cannot_be_deactivated()
    {
        var admin = await ApiClient.CreateAdminAsync(factory, acceptLanguage: "es");
        var matte = await Find(admin, "paints", "matte");

        var response = await admin.PutAsJsonAsync($"/api/admin/catalog/paints/{matte.GetProperty("id")}", new
        {
            name = matte.GetProperty("name"),
            pricePerLitreCents = matte.GetProperty("pricePerLitreCents").GetInt64(),
            coverageM2PerLitre = matte.GetProperty("coverageM2PerLitre").GetDecimal(),
            coats = matte.GetProperty("coats").GetInt32(),
            isActive = false,
            version = matte.GetProperty("version").GetUInt32(),
        }, ApiClient.Json);

        var problem = await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
        var message = problem.GetProperty("errors").GetProperty("isActive")[0].GetString();
        Assert.Contains("Dormitorio", message);
    }

    [Fact]
    public async Task A1_2_room_surfaces_cannot_be_deactivated_and_factor_is_bounded()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var wall = await Find(admin, "surfaces", "interior_wall");

        var response = await admin.PutAsJsonAsync($"/api/admin/catalog/surfaces/{wall.GetProperty("id")}", new
        {
            name = wall.GetProperty("name"),
            clientRateCents = 600,
            painterRateCents = 400,
            difficultyFactor = 3.5m,
            isActive = false,
            version = wall.GetProperty("version").GetUInt32(),
        }, ApiClient.Json);

        var problem = await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("difficultyFactor", out _));

        var deactivate = await admin.PutAsJsonAsync($"/api/admin/catalog/surfaces/{wall.GetProperty("id")}", new
        {
            name = wall.GetProperty("name"),
            clientRateCents = 600,
            painterRateCents = 400,
            difficultyFactor = 1m,
            isActive = false,
            version = wall.GetProperty("version").GetUInt32(),
        }, ApiClient.Json);
        problem = await ProblemAssert.HasCodeAsync(deactivate, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("isActive", out _));
    }

    [Fact]
    public async Task A1_3_room_type_needs_an_active_suggested_paint()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var sizes = new { s = new { length = 2m, width = 2m }, m = new { length = 3m, width = 2.5m }, l = new { length = 4m, width = 3m } };

        var invalid = await admin.PostAsJsonAsync("/api/admin/catalog/room-types", new
        {
            key = UniqueKey("room"),
            name = new { pt = "Escritório" },
            sizes,
            suggestedPaintId = Guid.NewGuid(),
            isActive = true,
        }, ApiClient.Json);
        var problem = await ProblemAssert.HasCodeAsync(invalid, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("suggestedPaintId", out _));

        var matte = await Find(admin, "paints", "matte");
        var created = await admin.PostAsJsonAsync("/api/admin/catalog/room-types", new
        {
            key = UniqueKey("room"),
            name = new { pt = "Escritório", es = "Despacho", en = "Office" },
            sizes,
            suggestedPaintId = matte.GetProperty("id").GetGuid(),
            isActive = true,
        }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var room = await created.ReadJsonAsync();
        Assert.Equal(2.5m, room.GetProperty("sizes").GetProperty("m").GetProperty("width").GetDecimal());
    }

    [Fact]
    public async Task A1_4_occupancy_modifiers_only_accept_known_keys()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var furnished = await Find(admin, "modifiers", "furnished");
        Assert.Equal("occupancy", furnished.GetProperty("group").GetString());
        Assert.Equal(1.1m, furnished.GetProperty("factor").GetDecimal());

        var response = await admin.PostAsJsonAsync("/api/admin/catalog/modifiers", new
        {
            key = "abandoned",
            name = new { pt = "Abandonada" },
            factor = 1.3m,
            isActive = true,
        }, ApiClient.Json);

        var problem = await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("key", out _));
    }

    [Fact]
    public async Task A1_6_settings_round_trip_and_validation()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var settings = await (await admin.GetAsync("/api/admin/settings")).ReadJsonAsync();
        Assert.Equal(15000, settings.GetProperty("minJobAmountCents").GetInt64());
        Assert.Equal(1500000, settings.GetProperty("maxJobAmountCents").GetInt64());
        Assert.Equal(23m, settings.GetProperty("vatPct").GetDecimal());

        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(settings.GetRawText())!;
        body["maxJobAmountCents"] = JsonSerializer.SerializeToElement(100);
        var invalid = await admin.PutAsJsonAsync("/api/admin/settings", body, ApiClient.Json);
        var problem = await ProblemAssert.HasCodeAsync(invalid, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("maxJobAmountCents", out _));

        // Saving the same values is a no-op for other tests but bumps the version.
        var saved = await admin.PutAsJsonAsync("/api/admin/settings", settings, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var stale = await admin.PutAsJsonAsync("/api/admin/settings", settings, ApiClient.Json);
        await ProblemAssert.HasCodeAsync(stale, HttpStatusCode.Conflict, "concurrency_conflict");
    }

    [Fact]
    public async Task A1_7_coverage_areas_are_seeded_and_editable()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var areas = await (await admin.GetAsync("/api/admin/coverage")).ReadJsonAsync();
        var municipalities = areas.EnumerateArray().Select(a => a.GetProperty("municipality").GetString()).Distinct().ToArray();
        Assert.Equal(16, municipalities.Length);

        var duplicate = await admin.PostAsJsonAsync("/api/admin/coverage",
            new { postalPrefix = "2710", municipality = "Sintra", isActive = true }, ApiClient.Json);
        await ProblemAssert.HasCodeAsync(duplicate, HttpStatusCode.UnprocessableEntity, "validation_failed");

        var prefix = Random.Shared.Next(9000, 9999).ToString();
        var created = await admin.PostAsJsonAsync("/api/admin/coverage",
            new { postalPrefix = prefix, municipality = "Teste", isActive = true }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var area = await created.ReadJsonAsync();

        var deactivated = await admin.PutAsJsonAsync($"/api/admin/coverage/{area.GetProperty("id")}",
            new { municipality = "Teste", isActive = false, version = area.GetProperty("version").GetUInt32() }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.False((await deactivated.ReadJsonAsync()).GetProperty("isActive").GetBoolean());
    }

    private static string UniqueKey(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..30];

    private static async Task<JsonElement> Find(HttpClient admin, string list, string key)
    {
        var items = await (await admin.GetAsync($"/api/admin/catalog/{list}")).ReadJsonAsync();
        return items.EnumerateArray().Single(i => i.GetProperty("key").GetString() == key);
    }
}
