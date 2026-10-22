using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace Pinta.Api.IntegrationTests;

/// <summary>Estimate and simulator against the seeded configuration; the numbers are the golden cases of docs/pricing-engine.md.</summary>
public class QuoteTests(PintaApiFactory factory)
{
    private static readonly bool[] AllWalls = [true, true, true, true];

    private static object Room(string type, string paint, decimal? length = null, decimal? width = null, decimal? height = null,
        string? size = null, bool[]? walls = null, bool ceiling = true, bool damage = false) => new
    {
        kind = "room",
        name = type,
        roomTypeId = type,
        size,
        length,
        width,
        height,
        walls = walls ?? AllWalls,
        ceiling,
        paintId = paint,
        damage,
    };

    private static object Surface(string type, string paint, decimal length, decimal width) => new
    {
        kind = "surface",
        surfaceTypeId = type,
        length,
        width,
        ceiling = false,
        paintId = paint,
        damage = false,
    };

    private static readonly object[] Flat =
    [
        Room("living", "matte", 5m, 4m),
        Room("kitchen", "satin", 3.5m, 3m),
        Room("bedroom", "matte", 4m, 3.5m),
        Room("bedroom", "matte", size: "M", walls: [true, true, true, false]),
        Room("bathroom", "anti_damp", 2.5m, 2m, damage: true),
        Room("hallway", "matte", 6m, 1.2m, ceiling: false),
    ];

    [Fact]
    public async Task C2_8_estimate_shows_area_and_range_but_no_internal_amounts()
    {
        var client = await ApiClient.CreateAsync(factory);

        var response = await client.PostAsJsonAsync("/api/quotes/estimate",
            new { occupancy = "empty", elements = new[] { Room("bedroom", "matte", 4m, 3m, 2.6m) } }, ApiClient.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        foreach (var hidden in new[] { "material", "labour", "painter", "margin", "subtotal", "rate", "deposit" })
        {
            Assert.DoesNotContain(hidden, body, StringComparison.OrdinalIgnoreCase);
        }

        var estimate = JsonDocument.Parse(body).RootElement;
        Assert.Equal(48.40m, estimate.GetProperty("areaM2").GetDecimal());
        var element = Assert.Single(estimate.GetProperty("elements").EnumerateArray());
        Assert.Equal(36.40m, element.GetProperty("wallsAreaM2").GetDecimal());
        Assert.Equal(12.00m, element.GetProperty("ceilingAreaM2").GetDecimal());
        Assert.Equal(44000, estimate.GetProperty("rangeMinCents").GetInt64());
        Assert.Equal(57000, estimate.GetProperty("rangeMaxCents").GetInt64());
        Assert.False(estimate.GetProperty("minimumApplied").GetBoolean());
        Assert.False(estimate.GetProperty("rangeWidened").GetBoolean());
    }

    [Fact]
    public async Task C2_8_estimate_flags_minimum_and_over_max()
    {
        var client = await ApiClient.CreateAsync(factory);

        var minimum = await Estimate(client, new { occupancy = "empty", elements = new[] { Surface("interior_wall", "matte", 3m, 2.6m) } });
        Assert.True(minimum.GetProperty("minimumApplied").GetBoolean());
        Assert.Equal(13500, minimum.GetProperty("rangeMinCents").GetInt64());
        Assert.Equal(17500, minimum.GetProperty("rangeMaxCents").GetInt64());

        var overMax = await Estimate(client, new { occupancy = "empty", elements = new[] { Surface("facade", "exterior", 40m, 20m) } });
        Assert.True(overMax.GetProperty("overMax").GetBoolean());
        Assert.True(overMax.GetProperty("premium").GetBoolean());
        Assert.Equal(JsonValueKind.Null, overMax.GetProperty("rangeMinCents").ValueKind);
        Assert.Equal(JsonValueKind.Null, overMax.GetProperty("rangeMaxCents").ValueKind);
    }

    [Fact]
    public async Task C2_8_TC13_room_without_walls_or_ceiling_returns_422_on_the_walls()
    {
        var client = await ApiClient.CreateAsync(factory, acceptLanguage: "es");

        var response = await client.PostAsJsonAsync("/api/quotes/estimate", new
        {
            occupancy = "empty",
            elements = new[] { Room("bedroom", "matte", 4m, 3m, walls: [false, false, false, false], ceiling: false) },
        }, ApiClient.Json);

        var problem = await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal("Selecciona al menos una pared o el techo.",
            problem.GetProperty("errors").GetProperty("elements[0].walls")[0].GetString());
    }

    [Fact]
    public async Task C2_8_unknown_catalog_items_and_bad_shapes_return_422_per_field()
    {
        var client = await ApiClient.CreateAsync(factory);

        var response = await client.PostAsJsonAsync("/api/quotes/estimate", new
        {
            occupancy = "empty",
            elements = new object[]
            {
                Room("bedroom", "gloss", 4m, 3m),
                new { kind = "room", roomTypeId = "bedroom", walls = new[] { true }, ceiling = true, paintId = "matte", damage = false },
            },
        }, ApiClient.Json);

        var problem = await ProblemAssert.HasCodeAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("elements[1].walls", out _));

        var unknown = await client.PostAsJsonAsync("/api/quotes/estimate",
            new { occupancy = "empty", elements = new[] { Room("bedroom", "gloss", 4m, 3m) } }, ApiClient.Json);
        problem = await ProblemAssert.HasCodeAsync(unknown, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("elements[0].paintId", out _));
    }

    [Fact]
    public async Task A1_8_simulator_returns_the_full_breakdown_of_TC01()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);

        var result = await Simulate(admin, new { occupancy = "empty", elements = new[] { Room("bedroom", "matte", 4m, 3m) } });

        var lines = result.GetProperty("lines").EnumerateArray().ToArray();
        Assert.Equal(2, lines.Length);
        Assert.Equal("interior_wall", lines[0].GetProperty("surface").GetString());
        Assert.Equal(36.40m, lines[0].GetProperty("areaM2").GetDecimal());
        Assert.Equal(8.01m, lines[0].GetProperty("litres").GetDecimal());
        Assert.Equal(6408, lines[0].GetProperty("materialCents").GetInt64());
        Assert.Equal(21840, lines[0].GetProperty("labourCents").GetInt64());
        Assert.Equal(14560, lines[0].GetProperty("painterCents").GetInt64());
        Assert.Equal(9660, lines[1].GetProperty("labourCents").GetInt64());
        Assert.Equal(8520, result.GetProperty("materialCents").GetInt64());
        Assert.Equal(31500, result.GetProperty("labourCents").GetInt64());
        Assert.Equal(40020, result.GetProperty("subtotalCents").GetInt64());
        Assert.Equal(9205, result.GetProperty("vatCents").GetInt64());
        Assert.Equal(49225, result.GetProperty("totalCents").GetInt64());
        Assert.Equal(21184, result.GetProperty("painterTotalCents").GetInt64());
        Assert.Equal(10316, result.GetProperty("marginCents").GetInt64());
        Assert.Equal(25.8m, result.GetProperty("marginPct").GetDecimal());
        Assert.Equal(14768, result.GetProperty("depositCents").GetInt64());
        Assert.Equal(34457, result.GetProperty("balanceCents").GetInt64());
        Assert.Equal(44000, result.GetProperty("rangeMinCents").GetInt64());
        Assert.Equal(57000, result.GetProperty("rangeMaxCents").GetInt64());
    }

    [Fact]
    public async Task A1_8_simulator_includes_extras_TC10()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);

        var result = await Simulate(admin, new
        {
            occupancy = "furnished",
            elements = Flat,
            extras = new[] { new { description = "Tratamento anti-humidade", clientPriceCents = 12195, painterPayCents = 7500 } },
        });

        Assert.Equal(266.44m, result.GetProperty("areaM2").GetDecimal());
        Assert.Equal(258917, result.GetProperty("subtotalCents").GetInt64());
        Assert.Equal(318468, result.GetProperty("totalCents").GetInt64());
        Assert.Equal(134767, result.GetProperty("painterTotalCents").GetInt64());
        Assert.Equal(66809, result.GetProperty("marginCents").GetInt64());
        Assert.Equal(286500, result.GetProperty("rangeMinCents").GetInt64());
        Assert.Equal(430000, result.GetProperty("rangeMaxCents").GetInt64());
        Assert.True(result.GetProperty("premium").GetBoolean());
        Assert.Equal("Tratamento anti-humidade", result.GetProperty("extras")[0].GetProperty("description").GetString());
    }

    [Fact]
    public async Task A1_8_simulator_flags_low_margin_TC07()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);

        var result = await Simulate(admin, new { occupancy = "empty", elements = new[] { Room("bathroom", "anti_damp", 2.5m, 2m, 2.4m, damage: true) } });

        Assert.Equal(19.5m, result.GetProperty("marginPct").GetDecimal());
        Assert.True(result.GetProperty("belowMinMargin").GetBoolean());
        Assert.True(result.GetProperty("rangeWidened").GetBoolean());
    }

    [Fact]
    public async Task A1_8_simulator_is_admin_only()
    {
        var body = new { occupancy = "empty", elements = new[] { Room("bedroom", "matte", 4m, 3m) } };
        var anonymous = await ApiClient.CreateAsync(factory);
        await ProblemAssert.HasCodeAsync(await anonymous.PostAsJsonAsync("/api/admin/quotes/simulate", body, ApiClient.Json),
            HttpStatusCode.Unauthorized, "unauthenticated");

        var client = await ApiClient.CreateAsync(factory);
        await client.RegisterAsync(TestData.UniqueEmail());
        await ProblemAssert.HasCodeAsync(await client.PostAsJsonAsync("/api/admin/quotes/simulate", body, ApiClient.Json),
            HttpStatusCode.Forbidden, "forbidden");
    }

    /// <summary>Exit criterion of H1: changing a rate in A1 changes the simulator result.</summary>
    [Fact]
    public async Task A1_2_A1_8_changing_a_surface_rate_changes_the_simulation()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var key = $"stone_{Guid.NewGuid():N}"[..20];
        var created = await admin.PostAsJsonAsync("/api/admin/catalog/surfaces", new
        {
            key,
            name = new { pt = "Pedra" },
            clientRateCents = 1000,
            painterRateCents = 700,
            difficultyFactor = 1m,
            isActive = true,
        }, ApiClient.Json);
        var surface = await created.ReadJsonAsync();
        var job = new { occupancy = "empty", elements = new[] { Surface(key, "matte", 10m, 5m) } };

        var before = await Simulate(admin, job);
        Assert.Equal(50000, before.GetProperty("labourCents").GetInt64());

        var updated = await admin.PutAsJsonAsync($"/api/admin/catalog/surfaces/{surface.GetProperty("id")}", new
        {
            name = new { pt = "Pedra" },
            clientRateCents = 1200,
            painterRateCents = 700,
            difficultyFactor = 1m,
            isActive = true,
            version = surface.GetProperty("version").GetUInt32(),
        }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var after = await Simulate(admin, job);
        Assert.Equal(60000, after.GetProperty("labourCents").GetInt64());
        Assert.True(after.GetProperty("totalCents").GetInt64() > before.GetProperty("totalCents").GetInt64());
    }

    [Fact]
    public async Task C2_8_estimate_is_limited_per_ip()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimits:EstimatePerMinute", "2"));
        var client = await ApiClient.CreateAsync(limited);
        var body = new { occupancy = "empty", elements = new[] { Room("bedroom", "matte", 4m, 3m) } };

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/quotes/estimate", body, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/quotes/estimate", body, ApiClient.Json)).StatusCode);
        var limitedResponse = await client.PostAsJsonAsync("/api/quotes/estimate", body, ApiClient.Json);

        await ProblemAssert.HasCodeAsync(limitedResponse, HttpStatusCode.TooManyRequests, "rate_limited");
        Assert.NotNull(limitedResponse.Headers.RetryAfter);
    }

    private static async Task<JsonElement> Estimate(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/quotes/estimate", body, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    private static async Task<JsonElement> Simulate(HttpClient admin, object body)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/quotes/simulate", body, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }
}
