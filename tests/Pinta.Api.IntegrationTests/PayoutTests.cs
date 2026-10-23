using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pinta.Api.IntegrationTests;

/// <summary>A5.4 (first half), P4.1: a closed job leaves one pending payout for the painter's pay.</summary>
public class PayoutTests(PintaApiFactory factory)
{
    [Fact]
    public async Task A5_4_P4_1_closing_the_job_creates_one_pending_payout_for_the_offer_pay()
    {
        var s = await TestAssignments.ScheduledAsync(factory);
        await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/start", new { }, ApiClient.Json);
        var finish = await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/finish",
            new { fileIds = await PainterJobTests.UploadResultsAsync(s.Painter, s.Job.JobId, 3) }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NoContent, finish.StatusCode);
        Assert.Equal(0, (await (await s.Painter.GetAsync("/api/painter/payouts")).ReadJsonAsync()).GetProperty("items").GetArrayLength());

        var balance = await (await TestPayments.CreateAsync(s.Job.Client, s.Job.JobId, "balance")).ReadJsonAsync();
        var paid = FakeStripe.Succeeded(TestPayments.IntentId(balance), balance.GetProperty("amountCents").GetInt64());
        await FakeStripe.SendAsync(factory.CreateClient(), "payment_intent.succeeded", paid);
        await FakeStripe.SendAsync(factory.CreateClient(), "payment_intent.succeeded", paid);

        Assert.Equal("closed", (await (await s.Job.Client.GetAsync($"/api/jobs/{s.Job.JobId}")).ReadJsonAsync()).GetProperty("status").GetString());
        var mine = await (await s.Painter.GetAsync("/api/painter/payouts")).ReadJsonAsync();
        var item = Assert.Single(mine.GetProperty("items").EnumerateArray());
        Assert.Equal((21184L, 0L), (mine.GetProperty("pendingCents").GetInt64(), mine.GetProperty("paidCents").GetInt64()));
        Assert.Equal((s.Job.Code, 21184L, "pending"), (item.GetProperty("code").GetString(), item.GetProperty("amountCents").GetInt64(), item.GetProperty("status").GetString()));
        var admin = await (await s.Admin.GetAsync($"/api/admin/payouts?painterId={s.PainterId}")).ReadJsonAsync();
        Assert.Equal(s.Job.JobId, Assert.Single(admin.EnumerateArray()).GetProperty("jobId").GetGuid());
    }

    [Fact]
    public async Task P4_1_the_month_is_validated_and_only_painters_see_their_payouts()
    {
        var s = await TestAssignments.ReadyAsync(factory);

        await ProblemAssert.HasCodeAsync(await s.Painter.GetAsync("/api/painter/payouts?month=2026-13"), HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal("2026-09", (await (await s.Painter.GetAsync("/api/painter/payouts?month=2026-09")).ReadJsonAsync()).GetProperty("month").GetString());
        await ProblemAssert.HasCodeAsync(await s.Job.Client.GetAsync("/api/painter/payouts"), HttpStatusCode.Forbidden, "forbidden");
        await ProblemAssert.HasCodeAsync(await s.Painter.GetAsync("/api/admin/payouts"), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task A5_4_the_admin_marks_a_payout_paid_and_the_painter_sees_it()
    {
        var s = await TestAssignments.ScheduledAsync(factory);
        await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/start", new { }, ApiClient.Json);
        await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/finish",
            new { fileIds = await PainterJobTests.UploadResultsAsync(s.Painter, s.Job.JobId, 3) }, ApiClient.Json);
        var balance = await (await TestPayments.CreateAsync(s.Job.Client, s.Job.JobId, "balance")).ReadJsonAsync();
        await FakeStripe.SendAsync(factory.CreateClient(), "payment_intent.succeeded",
            FakeStripe.Succeeded(TestPayments.IntentId(balance), balance.GetProperty("amountCents").GetInt64()));
        var payout = Assert.Single((await (await s.Admin.GetAsync($"/api/admin/payouts?painterId={s.PainterId}")).ReadJsonAsync()).EnumerateArray());
        var id = payout.GetProperty("id").GetGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var future = await s.Admin.PostAsJsonAsync($"/api/admin/payouts/{id}/mark-paid", new { paidOn = today.AddDays(2), reference = "TRF-1" }, ApiClient.Json);
        var noReference = await s.Admin.PostAsJsonAsync($"/api/admin/payouts/{id}/mark-paid", new { paidOn = today, reference = "" }, ApiClient.Json);
        var paid = await s.Admin.PostAsJsonAsync($"/api/admin/payouts/{id}/mark-paid", new { paidOn = today, reference = "TRF-2026-0042" }, ApiClient.Json);
        var again = await s.Admin.PostAsJsonAsync($"/api/admin/payouts/{id}/mark-paid", new { paidOn = today, reference = "TRF-2026-0043" }, ApiClient.Json);
        var byPainter = await s.Painter.PostAsJsonAsync($"/api/admin/payouts/{id}/mark-paid", new { paidOn = today, reference = "X" }, ApiClient.Json);

        await ProblemAssert.HasCodeAsync(future, HttpStatusCode.UnprocessableEntity, "validation_failed");
        await ProblemAssert.HasCodeAsync(noReference, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        await ProblemAssert.HasCodeAsync(again, HttpStatusCode.Conflict, "invalid_transition");
        await ProblemAssert.HasCodeAsync(byPainter, HttpStatusCode.Forbidden, "forbidden");
        var mine = await (await s.Painter.GetAsync("/api/painter/payouts")).ReadJsonAsync();
        var item = Assert.Single(mine.GetProperty("items").EnumerateArray());
        Assert.Equal(("paid", "TRF-2026-0042", 0L, 21184L), (item.GetProperty("status").GetString(), item.GetProperty("reference").GetString(),
            mine.GetProperty("pendingCents").GetInt64(), mine.GetProperty("paidCents").GetInt64()));
    }
}
