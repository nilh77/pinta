using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pinta.Api.IntegrationTests;

/// <summary>A3.1: the admin's list shows who paints the job and orders it by the last thing done with it.</summary>
public class JobActivityTests(PintaApiFactory factory)
{
    private static async Task<JsonElement> ListItemAsync(HttpClient admin, string code) =>
        Assert.Single((await (await admin.GetAsync($"/api/admin/jobs?q={code}")).ReadJsonAsync()).GetProperty("items").EnumerateArray());

    [Fact]
    public async Task A3_1_the_list_shows_the_offered_then_the_assigned_painter()
    {
        var s = await TestAssignments.ReadyAsync(factory);
        (await TestAssignments.OfferAsync(s.Admin, s.Job.JobId, s.PainterId, 3)).EnsureSuccessStatusCode();

        var offered = await ListItemAsync(s.Admin, s.Job.Code);
        await s.Painter.PostAsJsonAsync($"/api/painter/assignments/{await TestAssignments.OfferIdAsync(s.Painter)}/accept", new { }, ApiClient.Json);
        var accepted = await ListItemAsync(s.Admin, s.Job.Code);

        Assert.True(offered.GetProperty("painterOffered").GetBoolean());
        Assert.False(string.IsNullOrEmpty(offered.GetProperty("painterName").GetString()));
        Assert.False(accepted.GetProperty("painterOffered").GetBoolean());
        Assert.Equal(offered.GetProperty("painterName").GetString(), accepted.GetProperty("painterName").GetString());
    }

    [Fact]
    public async Task A3_1_progress_photos_and_a_question_count_as_activity_without_touching_the_job()
    {
        var s = await TestAssignments.ScheduledAsync(factory);
        await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/start", new { }, ApiClient.Json);
        var before = await ListItemAsync(s.Admin, s.Job.Code);

        var upload = await (await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/media/upload-url",
            new { contentType = "image/jpeg", sizeBytes = 2048, context = "progress" }, ApiClient.Json)).ReadJsonAsync();
        Assert.True((await MediaTests.UploadToStorageAsync(upload, new byte[2048], "image/jpeg")).IsSuccessStatusCode);
        var added = await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/progress",
            new { fileIds = new[] { upload.GetProperty("fileId").GetGuid() } }, ApiClient.Json);
        var after = await ListItemAsync(s.Admin, s.Job.Code);

        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
        Assert.True(after.GetProperty("lastActivityAt").GetDateTimeOffset() > before.GetProperty("lastActivityAt").GetDateTimeOffset());
        // The job row itself is untouched: no state change, no concurrency conflict for someone editing it.
        Assert.Equal(before.GetProperty("updatedAt").GetDateTimeOffset(), after.GetProperty("updatedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task A3_1_the_most_recently_worked_on_job_comes_first()
    {
        var first = await TestJobs.SendAsync(factory);
        var second = await TestJobs.SendAsync(factory);
        var admin = await ApiClient.CreateAdminAsync(factory);

        // The older job gets a price draft saved: it moves above the newer one.
        var quote = (await (await admin.GetAsync($"/api/admin/jobs/{first.JobId}")).ReadJsonAsync()).GetProperty("currentQuote");
        var body = new JsonObject
        {
            ["occupancy"] = quote.GetProperty("occupancy").GetString(),
            ["elements"] = JsonNode.Parse(quote.GetProperty("elements").GetRawText()),
            ["extras"] = new JsonArray(),
            ["premium"] = null,
            ["manualSubtotalCents"] = null,
            ["adjustmentReason"] = null,
        };
        var draft = await admin.PutAsJsonAsync($"/api/admin/jobs/{first.JobId}/draft-quote", body, ApiClient.Json);
        var list = await (await admin.GetAsync("/api/admin/jobs?status=in_review&pageSize=100")).ReadJsonAsync();
        var codes = list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("code").GetString()).ToList();

        Assert.True(draft.IsSuccessStatusCode, await draft.Content.ReadAsStringAsync());
        Assert.True(codes.IndexOf(first.Code) < codes.IndexOf(second.Code));
    }
}
