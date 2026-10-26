using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pinta.Modules.Finance.Data;
using Pinta.Modules.Jobs.Data;

namespace Pinta.Api.IntegrationTests;

/// <summary>
/// A5.1–A5.3: the month's figures, operating costs and closing. The database is shared by every test, so each test
/// moves its own jobs into a month of its own, long past, before reading the figures.
/// </summary>
public class FinanceTests(PintaApiFactory factory)
{
    /// <summary>A month of 2001–2020 that no other test uses.</summary>
    private static string OwnMonth()
    {
        var n = Random.Shared.Next(0, 240);
        return $"{2001 + n / 12}-{n % 12 + 1:00}";
    }

    private async Task MoveToMonthAsync(Guid jobId, string month)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var day = new DateTimeOffset(DateOnly.Parse($"{month}-15").ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
        await scope.ServiceProvider.GetRequiredService<JobsDbContext>().Database
            .ExecuteSqlInterpolatedAsync($"UPDATE jobs.status_history SET changed_at = {day} WHERE job_id = {jobId} AND \"to\" IN ('closed', 'cancelled')");
    }

    /// <summary>TC01 done by a painter and its balance paid: closed, with a pending payout of 211,84 €.</summary>
    private async Task<(Guid JobId, Guid PainterId)> ClosedAsync()
    {
        var s = await TestAssignments.ScheduledAsync(factory);
        await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/start", new { }, ApiClient.Json);
        await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/finish",
            new { fileIds = await PainterJobTests.UploadResultsAsync(s.Painter, s.Job.JobId, 3) }, ApiClient.Json);
        var balance = await (await TestPayments.CreateAsync(s.Job.Client, s.Job.JobId, "balance")).ReadJsonAsync();
        await FakeStripe.SendAsync(factory.CreateClient(), "payment_intent.succeeded",
            FakeStripe.Succeeded(TestPayments.IntentId(balance), balance.GetProperty("amountCents").GetInt64()));
        return (s.Job.JobId, s.PainterId);
    }

    /// <summary>TC01 cancelled by the client 3 days before the start: 73,84 € of the 147,68 € deposit kept.</summary>
    private async Task<Guid> CancelledLateAsync()
    {
        var (job, _) = await TestPayments.PaidDepositAsync(factory);
        var admin = await ApiClient.CreateAdminAsync(factory);
        await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/transition",
            new { to = "scheduled", reason = "Pintor confirmado", scheduledOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3) }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NoContent, (await job.Client.PostAsJsonAsync($"/api/jobs/{job.JobId}/cancel", new { reason = "Mudança." }, ApiClient.Json)).StatusCode);
        return job.JobId;
    }

    private static Task<HttpResponseMessage> AddCostAsync(HttpClient admin, string date, string category, long cents) =>
        admin.PostAsJsonAsync("/api/admin/finance/costs", new { date, category, description = $"{category} {date}", amountCents = cents }, ApiClient.Json);

    [Fact]
    public async Task A5_1_A5_3_the_month_matches_the_hand_calculation_and_closing_freezes_it()
    {
        var month = OwnMonth();
        var admin = await ApiClient.CreateAdminAsync(factory);
        var (closed, painterId) = await ClosedAsync();
        var cancelled = await CancelledLateAsync();
        await MoveToMonthAsync(closed, month);
        await MoveToMonthAsync(cancelled, month);
        // The painter is actually paid 200,00 € instead of the 211,84 € of the quote: the payout counts.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Database
                .ExecuteSqlInterpolatedAsync($"UPDATE finance.painter_payouts SET amount = 200.00 WHERE job_id = {closed}");
        }

        Assert.Equal(HttpStatusCode.OK, (await AddCostAsync(admin, $"{month}-03", "infrastructure", 2500)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AddCostAsync(admin, $"{month}-20", "payment_fees", 600)).StatusCode);

        var summary = await (await admin.GetAsync($"/api/admin/finance/summary?month={month}")).ReadJsonAsync();

        // By hand (docs/pricing-engine.md TC01; docs/data-model.md "Cifras del mes"):
        //   closed TC01: income 400,20, VAT 92,05, material 85,20, painter 200,00 → margin 115,00
        //   cancelled TC01: kept 73,84 → income R2(73,84 × 400,20 ÷ 492,25) = 60,03, VAT 13,81 → margin 60,03
        //   income 460,23 · VAT 105,86 · material 85,20 · painter 200,00 · margin 175,03 (38,0%)
        //   costs 25,00 + 6,00 = 31,00 · profit 144,03 · each partner R2(72,015) = 72,02
        Assert.Equal((46023L, 10586L, 8520L, 20000L, 17503L, 38.0m), (summary.GetProperty("incomeCents").GetInt64(), summary.GetProperty("vatCents").GetInt64(),
            summary.GetProperty("materialCents").GetInt64(), summary.GetProperty("painterPayCents").GetInt64(),
            summary.GetProperty("grossMarginCents").GetInt64(), summary.GetProperty("marginPct").GetDecimal()));
        Assert.Equal((3100L, 14403L, 7202L, false, true), (summary.GetProperty("costsCents").GetInt64(), summary.GetProperty("profitCents").GetInt64(),
            summary.GetProperty("perPartnerCents").GetInt64(), summary.GetProperty("closed").GetBoolean(), summary.GetProperty("canClose").GetBoolean()));
        Assert.Equal(2, summary.GetProperty("jobs").GetArrayLength());
        var cancelledLine = summary.GetProperty("jobs").EnumerateArray().Single(j => j.GetProperty("jobId").GetGuid() == cancelled);
        Assert.Equal((6003L, 1381L, 0L), (cancelledLine.GetProperty("incomeCents").GetInt64(), cancelledLine.GetProperty("vatCents").GetInt64(),
            cancelledLine.GetProperty("painterPayCents").GetInt64()));

        var csv = await (await admin.GetAsync($"/api/admin/finance/export?month={month}")).Content.ReadAsStringAsync();
        var rows = csv.TrimStart('\uFEFF').TrimEnd('\n').Split('\n');
        Assert.Equal(1 + 2 + 2 + 1, rows.Length);
        Assert.Equal(2, rows.Count(r => r.StartsWith("cost,", StringComparison.Ordinal)));
        Assert.Equal($"total,{month},,,,460.23,105.86,85.20,200.00,175.03,31.00,144.03,72.02", rows[^1]);

        var close = await admin.PostAsJsonAsync("/api/admin/finance/closings", new { month }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        var frozen = await close.ReadJsonAsync();
        Assert.Equal((true, 14403L, 7202L), (frozen.GetProperty("closed").GetBoolean(), frozen.GetProperty("profitCents").GetInt64(),
            frozen.GetProperty("perPartnerCents").GetInt64()));

        // A closed month accepts no changes, and the payout amount changing later does not move the frozen figures.
        var cost = summary.GetProperty("costs")[0];
        await ProblemAssert.HasCodeAsync(await AddCostAsync(admin, $"{month}-10", "marketing", 1000), HttpStatusCode.UnprocessableEntity, "period_closed");
        await ProblemAssert.HasCodeAsync(await admin.PutAsJsonAsync($"/api/admin/finance/costs/{cost.GetProperty("id").GetGuid()}",
            new { date = $"{month}-03", category = "infrastructure", description = "x", amountCents = 1 }, ApiClient.Json), HttpStatusCode.UnprocessableEntity, "period_closed");
        await ProblemAssert.HasCodeAsync(await admin.DeleteAsync($"/api/admin/finance/costs/{cost.GetProperty("id").GetGuid()}"),
            HttpStatusCode.UnprocessableEntity, "period_closed");
        await ProblemAssert.HasCodeAsync(await admin.PostAsJsonAsync("/api/admin/finance/closings", new { month }, ApiClient.Json),
            HttpStatusCode.UnprocessableEntity, "period_closed");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Database
                .ExecuteSqlInterpolatedAsync($"UPDATE finance.painter_payouts SET amount = 150.00 WHERE job_id = {closed}");
        }

        var after = await (await admin.GetAsync($"/api/admin/finance/summary?month={month}")).ReadJsonAsync();
        Assert.Equal((14403L, false), (after.GetProperty("profitCents").GetInt64(), after.GetProperty("canClose").GetBoolean()));
        Assert.NotEqual(Guid.Empty, painterId);
    }

    [Fact]
    public async Task A5_2_A5_3_costs_are_validated_and_only_a_finished_month_closes()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await ProblemAssert.HasCodeAsync(await AddCostAsync(admin, today.AddDays(2).ToString("yyyy-MM-dd"), "insurance", 1000),
            HttpStatusCode.UnprocessableEntity, "validation_failed");
        await ProblemAssert.HasCodeAsync(await AddCostAsync(admin, today.ToString("yyyy-MM-dd"), "insurance", 0), HttpStatusCode.UnprocessableEntity, "validation_failed");
        var added = await (await AddCostAsync(admin, today.ToString("yyyy-MM-dd"), "insurance", 4200)).ReadJsonAsync();
        var edited = await admin.PutAsJsonAsync($"/api/admin/finance/costs/{added.GetProperty("id").GetGuid()}",
            new { date = today, category = "insurance", description = "Seguro RC", amountCents = 4300, version = added.GetProperty("version").GetUInt32() }, ApiClient.Json);
        var stale = await admin.PutAsJsonAsync($"/api/admin/finance/costs/{added.GetProperty("id").GetGuid()}",
            new { date = today, category = "insurance", description = "Seguro RC", amountCents = 4400, version = added.GetProperty("version").GetUInt32() }, ApiClient.Json);

        Assert.Equal(4300, (await edited.ReadJsonAsync()).GetProperty("amountCents").GetInt64());
        await ProblemAssert.HasCodeAsync(stale, HttpStatusCode.Conflict, "concurrency_conflict");
        await ProblemAssert.HasCodeAsync(await admin.PostAsJsonAsync("/api/admin/finance/closings", new { month = today.ToString("yyyy-MM") }, ApiClient.Json),
            HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/finance/costs/{added.GetProperty("id").GetGuid()}")).StatusCode);
    }

    [Fact]
    public async Task A5_finance_is_for_admins_only()
    {
        var s = await TestAssignments.ReadyAsync(factory);

        await ProblemAssert.HasCodeAsync(await s.Painter.GetAsync("/api/admin/finance/summary"), HttpStatusCode.Forbidden, "forbidden");
        await ProblemAssert.HasCodeAsync(await s.Job.Client.GetAsync("/api/admin/finance/summary"), HttpStatusCode.Forbidden, "forbidden");
    }
}

/// <summary>H7 exit criterion: the demo's closed month matches the hand calculation in docs/plan.md («Datos de demo» › Finanzas).</summary>
public class DemoFinanceTests(PintaApiFactory factory)
{
    [Fact]
    public async Task H7_the_seeded_last_month_is_closed_and_matches_the_hand_calculation()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var lisbon = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Pinta.SharedKernel.Time.BusinessDays.Lisbon).DateTime);
        var month = new DateOnly(lisbon.Year, lisbon.Month, 1).AddMonths(-1).ToString("yyyy-MM");

        var summary = await (await admin.GetAsync($"/api/admin/finance/summary?month={month}")).ReadJsonAsync();

        Assert.Equal((true, 164743L, 37891L, 26016L, 88800L, 49927L), (summary.GetProperty("closed").GetBoolean(), summary.GetProperty("incomeCents").GetInt64(),
            summary.GetProperty("vatCents").GetInt64(), summary.GetProperty("materialCents").GetInt64(), summary.GetProperty("painterPayCents").GetInt64(),
            summary.GetProperty("grossMarginCents").GetInt64()));
        Assert.Equal((6393L, 43534L, 21767L), (summary.GetProperty("costsCents").GetInt64(), summary.GetProperty("profitCents").GetInt64(),
            summary.GetProperty("perPartnerCents").GetInt64()));
        Assert.Equal(["0091", "0093", "0092"], summary.GetProperty("jobs").EnumerateArray().Select(j => j.GetProperty("code").GetString()![^4..]));
        await ProblemAssert.HasCodeAsync(await admin.PostAsJsonAsync("/api/admin/finance/costs",
            new { date = $"{month}-15", category = "marketing", description = "Folhetos", amountCents = 5000 }, ApiClient.Json), System.Net.HttpStatusCode.UnprocessableEntity, "period_closed");
    }
}

/// <summary>H8: the demo jobs of docs/plan.md («Datos de demo» › Trabajos), one in every state, with their photos.</summary>
public class DemoJobsTests(PintaApiFactory factory)
{
    [Theory]
    [InlineData("0101", "in_review", 4)]
    [InlineData("0102", "in_review", 0)]
    [InlineData("0103", "site_visit", 0)]
    [InlineData("0104", "quoted", 0)]
    [InlineData("0105", "deposit_paid", 0)]
    [InlineData("0106", "scheduled", 0)]
    [InlineData("0107", "in_progress", 2)]
    [InlineData("0108", "finished", 3)]
    [InlineData("0094", "expired", 0)]
    [InlineData("0095", "rejected", 0)]
    public async Task H8_each_demo_job_is_in_its_state(string number, string status, int files)
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var lisbon = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Pinta.SharedKernel.Time.BusinessDays.Lisbon).DateTime);
        var code = $"PT-{lisbon.Year}-{number}";

        var list = await (await admin.GetAsync($"/api/admin/jobs?q={code}")).ReadJsonAsync();
        var id = Assert.Single(list.GetProperty("items").EnumerateArray(), j => j.GetProperty("code").GetString() == code).GetProperty("id").GetGuid();
        var detail = await (await admin.GetAsync($"/api/admin/jobs/{id}")).ReadJsonAsync();

        Assert.Equal((status, files), (detail.GetProperty("status").GetString(), detail.GetProperty("files").GetArrayLength()));
        switch (number)
        {
            case "0102":
                Assert.Equal("amber", detail.GetProperty("light").GetString());
                break;
            case "0103":
                Assert.True(detail.GetProperty("premium").GetBoolean());
                Assert.Equal("Rui Almeida", detail.GetProperty("siteVisit").GetProperty("assigneeName").GetString());
                break;
            case "0106":
                Assert.Equal(lisbon.ToString("yyyy-MM-dd"), detail.GetProperty("scheduledOn").GetString());
                Assert.Equal("Rui Almeida", Assert.Single(detail.GetProperty("assignments").EnumerateArray()).GetProperty("painterName").GetString());
                break;
        }
    }
}
