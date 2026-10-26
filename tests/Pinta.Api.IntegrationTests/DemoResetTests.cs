using System.Net;
using System.Net.Http.Json;

namespace Pinta.Api.IntegrationTests;

/// <summary>
/// H8: «Restablecer datos de demo». Runs on its own database and storage: a reset on the shared ones would empty them
/// under the other tests.
/// </summary>
public class DemoResetTests
{
    [Fact]
    public async Task H8_the_reset_brings_the_demo_back_and_keeps_the_admin_signed_in()
    {
        await using var own = new PintaApiFactory();
        await own.InitializeAsync();
        var admin = await ApiClient.CreateAdminAsync(own);
        var year = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Pinta.SharedKernel.Time.BusinessDays.Lisbon).DateTime).Year;
        var extra = await TestJobs.SendAsync(own);
        Assert.Equal($"PT-{year}-0121", extra.Code);

        await ProblemAssert.HasCodeAsync(await extra.Client.PostAsJsonAsync("/api/admin/demo/reset", new { }, ApiClient.Json), HttpStatusCode.Forbidden, "forbidden");
        var reset = await admin.PostAsJsonAsync("/api/admin/demo/reset", new { }, ApiClient.Json);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        // The admin is still signed in and sees the demo jobs, not the request made before the reset.
        var codes = (await (await admin.GetAsync("/api/admin/jobs?pageSize=100")).ReadJsonAsync()).GetProperty("items").EnumerateArray()
            .Select(j => j.GetProperty("code").GetString()).ToList();
        Assert.Contains($"PT-{year}-0101", codes);
        Assert.DoesNotContain(extra.Code, codes);
        var fresh = await ApiClient.CreateAsync(own);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.LoginAsync(extra.Email, TestData.Password)).StatusCode);
        // Numbering starts again after the demo jobs, and last month still closes as calculated by hand.
        Assert.Equal($"PT-{year}-0121", (await TestJobs.SendAsync(own)).Code);
        var lisbon = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Pinta.SharedKernel.Time.BusinessDays.Lisbon).DateTime);
        var month = new DateOnly(lisbon.Year, lisbon.Month, 1).AddMonths(-1).ToString("yyyy-MM");
        var summary = await (await admin.GetAsync($"/api/admin/finance/summary?month={month}")).ReadJsonAsync();
        Assert.Equal((true, 43534L), (summary.GetProperty("closed").GetBoolean(), summary.GetProperty("profitCents").GetInt64()));
    }
}
