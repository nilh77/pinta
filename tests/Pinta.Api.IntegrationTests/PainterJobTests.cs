using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pinta.Modules.Notifications.Contracts;

namespace Pinta.Api.IntegrationTests;

/// <summary>P1.3, P2.1, P2.2, C5.4, C5.5: the painter's job from detail to finished, as the client sees it.</summary>
public class PainterJobTests(PintaApiFactory factory)
{
    internal static async Task<List<Guid>> UploadResultsAsync(HttpClient painter, Guid jobId, int count)
    {
        var ids = new List<Guid>();
        var content = new byte[2048];
        for (var i = 0; i < count; i++)
        {
            var upload = await (await painter.PostAsJsonAsync($"/api/painter/jobs/{jobId}/media/upload-url",
                new { contentType = "image/jpeg", sizeBytes = content.Length }, ApiClient.Json)).ReadJsonAsync();
            Assert.True((await MediaTests.UploadToStorageAsync(upload, content, "image/jpeg")).IsSuccessStatusCode);
            ids.Add(upload.GetProperty("fileId").GetGuid());
        }

        return ids;
    }

    [Fact]
    public async Task P1_3_the_painter_sees_what_to_paint_and_their_pay_but_never_the_client_price()
    {
        var s = await TestAssignments.ScheduledAsync(factory, inDays: 2);

        var detail = await (await s.Painter.GetAsync($"/api/painter/jobs/{s.Job.JobId}")).ReadJsonAsync();

        Assert.Equal((s.Job.Code, "scheduled", 21184L, 48.4m, false), (detail.GetProperty("code").GetString(), detail.GetProperty("status").GetString(),
            detail.GetProperty("painterPayCents").GetInt64(), detail.GetProperty("areaM2").GetDecimal(), detail.GetProperty("canStart").GetBoolean()));
        Assert.False(string.IsNullOrEmpty(detail.GetProperty("clientName").GetString()));
        Assert.Equal(JsonValueKind.String, detail.GetProperty("clientPhone").ValueKind);
        var elements = detail.GetProperty("elements").EnumerateArray().ToList();
        Assert.NotEmpty(elements);
        Assert.Equal(detail.GetProperty("paints").EnumerateArray().Sum(p => p.GetProperty("litres").GetDecimal()),
            elements.Sum(e => e.GetProperty("litres").GetDecimal()));
        Assert.True(elements.Sum(e => e.GetProperty("litres").GetDecimal()) > 0);
    }

    [Fact]
    public async Task P1_3_offers_and_other_painters_jobs_are_not_found()
    {
        var s = await TestAssignments.ReadyAsync(factory);
        await TestAssignments.OfferAsync(s.Admin, s.Job.JobId, s.PainterId);
        var (_, _, otherLink) = await TestPainterAccounts.CreateAsync(factory, s.Admin);
        var other = await TestPainterAccounts.SignUpAsync(factory, otherLink);

        await ProblemAssert.HasCodeAsync(await s.Painter.GetAsync($"/api/painter/jobs/{s.Job.JobId}"), HttpStatusCode.NotFound, "not_found");
        await s.Painter.PostAsJsonAsync($"/api/painter/assignments/{await TestAssignments.OfferIdAsync(s.Painter)}/accept", new { }, ApiClient.Json);
        await ProblemAssert.HasCodeAsync(await other.GetAsync($"/api/painter/jobs/{s.Job.JobId}"), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.HasCodeAsync(await other.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/start", new { }, ApiClient.Json), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.HasCodeAsync(await s.Job.Client.GetAsync($"/api/painter/jobs/{s.Job.JobId}"), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task P2_1_the_job_cannot_start_before_its_day()
    {
        var s = await TestAssignments.ScheduledAsync(factory, inDays: 2);

        await ProblemAssert.HasCodeAsync(await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/start", new { }, ApiClient.Json),
            HttpStatusCode.Conflict, "invalid_transition");
    }

    [Fact]
    public async Task P2_2_C5_4_C5_5_finishing_needs_three_result_photos_and_the_client_sees_them()
    {
        var s = await TestAssignments.ScheduledAsync(factory);
        var scheduled = await (await s.Job.Client.GetAsync($"/api/jobs/{s.Job.JobId}")).ReadJsonAsync();
        Assert.Equal("Rui", scheduled.GetProperty("painter").GetProperty("firstName").GetString());

        var start = await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/start", new { }, ApiClient.Json);
        var photos = await UploadResultsAsync(s.Painter, s.Job.JobId, 3);
        var two = await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/finish", new { fileIds = photos.Take(2) }, ApiClient.Json);
        var three = await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/finish", new { fileIds = photos }, ApiClient.Json);
        var again = await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/finish", new { fileIds = photos }, ApiClient.Json);

        Assert.Equal(HttpStatusCode.NoContent, start.StatusCode);
        await ProblemAssert.HasCodeAsync(two, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal(HttpStatusCode.NoContent, three.StatusCode);
        await ProblemAssert.HasCodeAsync(again, HttpStatusCode.Conflict, "invalid_transition");
        var finished = await (await s.Job.Client.GetAsync($"/api/jobs/{s.Job.JobId}")).ReadJsonAsync();
        Assert.Equal("finished", finished.GetProperty("status").GetString());
        Assert.Equal(photos.Order(), finished.GetProperty("resultPhotos").EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).Order());
        var email = Assert.Single(factory.Emails.To<JobFinishedEmail>(s.Job.Email));
        Assert.Equal(finished.GetProperty("quote").GetProperty("balanceCents").GetInt64(), email.BalanceCents);
        var jobs = await (await s.Painter.GetAsync("/api/painter/jobs")).ReadJsonAsync();
        Assert.Equal("completed", Assert.Single(jobs.GetProperty("done").EnumerateArray()).GetProperty("status").GetString());
        var detail = await (await s.Painter.GetAsync($"/api/painter/jobs/{s.Job.JobId}")).ReadJsonAsync();
        Assert.Equal(3, detail.GetProperty("resultFiles").GetArrayLength());
    }

    /// <summary>CLAUDE.md rule 8 and the H6 exit criterion: no painter response carries the client price, VAT, deposit, rates or margin.</summary>
    [Fact]
    public async Task Painter_responses_never_carry_the_client_price_or_the_margin()
    {
        string[] forbidden = ["total", "subtotal", "vat", "deposit", "balance", "margin", "clientprice", "material", "rate"];
        var s = await TestAssignments.ScheduledAsync(factory);
        await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/start", new { }, ApiClient.Json);
        await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/finish",
            new { fileIds = await UploadResultsAsync(s.Painter, s.Job.JobId, 3) }, ApiClient.Json);

        foreach (var path in new[] { "/api/painter/jobs", $"/api/painter/jobs/{s.Job.JobId}", "/api/painter/me", "/api/painter/payouts" })
        {
            var json = await (await s.Painter.GetAsync(path)).ReadJsonAsync();
            var names = PropertyNames(json).ToList();
            Assert.NotEmpty(names);
            Assert.DoesNotContain(names, n => forbidden.Any(f => n.Contains(f, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private static IEnumerable<string> PropertyNames(JsonElement json) => json.ValueKind switch
    {
        JsonValueKind.Object => json.EnumerateObject().SelectMany(p => PropertyNames(p.Value).Prepend(p.Name)),
        JsonValueKind.Array => json.EnumerateArray().SelectMany(PropertyNames),
        _ => [],
    };
}
