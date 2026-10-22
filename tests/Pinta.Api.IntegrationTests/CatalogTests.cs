using System.Net;
using System.Text.Json;

namespace Pinta.Api.IntegrationTests;

public class CatalogTests(PintaApiFactory factory)
{
    [Fact]
    public async Task C2_public_catalog_lists_the_seed_options_without_rates()
    {
        var client = await ApiClient.CreateAsync(factory, acceptLanguage: "es");

        var response = await client.GetAsync("/api/catalog");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("cents", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rate", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("factor", body, StringComparison.OrdinalIgnoreCase);

        var catalog = JsonDocument.Parse(body).RootElement;
        Assert.Equal(2.6m, catalog.GetProperty("defaultHeight").GetDecimal());
        var bedroom = catalog.GetProperty("roomTypes").EnumerateArray().Single(r => r.GetProperty("id").GetString() == "bedroom");
        Assert.Equal("Dormitorio", bedroom.GetProperty("name").GetString());
        Assert.Equal("matte", bedroom.GetProperty("suggestedPaintId").GetString());
        var m = bedroom.GetProperty("sizes").EnumerateArray().Single(s => s.GetProperty("size").GetString() == "M");
        Assert.Equal(3.5m, m.GetProperty("length").GetDecimal());
        Assert.Equal(3m, m.GetProperty("width").GetDecimal());
        Assert.Contains("anti_damp", Ids(catalog, "paints"));
        Assert.Contains("facade", Ids(catalog, "surfaceTypes"));
        Assert.Equal(["empty", "furnished", "inhabited"], Ids(catalog, "occupancies"));
    }

    private static string[] Ids(JsonElement catalog, string list) =>
        catalog.GetProperty(list).EnumerateArray().Select(o => o.GetProperty("id").GetString()!).ToArray();
}
