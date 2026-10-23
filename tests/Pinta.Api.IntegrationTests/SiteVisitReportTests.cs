using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pinta.Modules.Notifications.Contracts;

namespace Pinta.Api.IntegrationTests;

/// <summary>A2.6, P3.1: a site visit done by a painter whose report becomes the review draft.</summary>
public class SiteVisitReportTests(PintaApiFactory factory)
{
    private sealed record Visit(TestJobs.SentJob Job, HttpClient Admin, HttpClient Painter, Guid PainterUserId, string PainterEmail);

    /// <summary>TC01 in review, its site visit assigned to a painter covering Sintra.</summary>
    private async Task<Visit> AssignedAsync()
    {
        var job = await TestJobs.SendAsync(factory);
        var admin = await ApiClient.CreateAdminAsync(factory);
        var (_, email, link) = await TestPainterAccounts.CreateAsync(factory, admin);
        var painter = await TestPainterAccounts.SignUpAsync(factory, link);
        var userId = Guid.Parse(link["userId"]);
        var response = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/site-visit",
            new { assigneeId = userId, at = DateTimeOffset.UtcNow.AddDays(2) }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return new Visit(job, admin, painter, userId, email);
    }

    private static async Task<JsonNode> ReportBodyAsync(HttpClient painter, Guid jobId, IEnumerable<Guid> fileIds)
    {
        var detail = await (await painter.GetAsync($"/api/painter/visits/{jobId}")).ReadJsonAsync();
        var elements = JsonNode.Parse(detail.GetProperty("elements").GetRawText())!.AsArray();
        foreach (var e in elements)
        {
            var o = e!.AsObject();
            foreach (var key in new[] { "id", "areaM2", "litres" })
            {
                o.Remove(key);
            }

            o["length"] = 5m;
        }

        return new JsonObject
        {
            ["elements"] = elements,
            ["extras"] = new JsonArray(new JsonObject { ["description"] = "Reparar fissura na parede B", ["painterPayCents"] = 4000 }),
            ["fileIds"] = JsonSerializer.SerializeToNode(fileIds, ApiClient.Json),
            ["notes"] = "Parede B com humidade.",
        };
    }

    [Fact]
    public async Task A2_6_P3_1_the_painter_reports_the_visit_and_the_draft_has_the_real_measures()
    {
        var v = await AssignedAsync();

        var assignees = await (await v.Admin.GetAsync($"/api/admin/jobs/{v.Job.JobId}/site-visit-assignees")).ReadJsonAsync();
        Assert.Contains(assignees.EnumerateArray(), a => a.GetProperty("userId").GetGuid() == v.PainterUserId && a.GetProperty("isPainter").GetBoolean());
        Assert.Single(factory.Emails.To<SiteVisitAssignedEmail>(v.PainterEmail));
        Assert.Empty(factory.Emails.To<SiteVisitScheduledEmail>(v.PainterEmail));
        Assert.Single(factory.Emails.To<SiteVisitScheduledEmail>(v.Job.Email));
        var list = await (await v.Painter.GetAsync("/api/painter/jobs")).ReadJsonAsync();
        Assert.Equal(v.Job.JobId, Assert.Single(list.GetProperty("visits").EnumerateArray()).GetProperty("jobId").GetGuid());

        var upload = await (await v.Painter.PostAsJsonAsync($"/api/painter/visits/{v.Job.JobId}/media/upload-url",
            new { contentType = "image/jpeg", sizeBytes = 2048 }, ApiClient.Json)).ReadJsonAsync();
        Assert.True((await MediaTests.UploadToStorageAsync(upload, new byte[2048], "image/jpeg")).IsSuccessStatusCode);
        var body = await ReportBodyAsync(v.Painter, v.Job.JobId, [upload.GetProperty("fileId").GetGuid()]);
        var report = await v.Painter.PostAsJsonAsync($"/api/painter/visits/{v.Job.JobId}/report", body, ApiClient.Json);
        var again = await v.Painter.PostAsJsonAsync($"/api/painter/visits/{v.Job.JobId}/report", body, ApiClient.Json);

        Assert.Equal(HttpStatusCode.NoContent, report.StatusCode);
        await ProblemAssert.HasCodeAsync(again, HttpStatusCode.Conflict, "invalid_transition");
        var detail = await (await v.Admin.GetAsync($"/api/admin/jobs/{v.Job.JobId}")).ReadJsonAsync();
        Assert.Equal("in_review", detail.GetProperty("status").GetString());
        var draft = detail.GetProperty("draft");
        Assert.Equal("site_visit", draft.GetProperty("origin").GetString());
        Assert.Equal(5m, draft.GetProperty("elements")[0].GetProperty("length").GetDecimal());
        var extra = Assert.Single(draft.GetProperty("extras").EnumerateArray());
        Assert.Equal((4000L, true), (extra.GetProperty("painterPayCents").GetInt64(), extra.GetProperty("clientPriceToSet").GetBoolean()));
        Assert.Equal("Parede B com humidade.", detail.GetProperty("siteVisit").GetProperty("reportNotes").GetString());
        Assert.Contains(detail.GetProperty("files").EnumerateArray(), f => f.GetProperty("context").GetString() == "site_visit");
        Assert.Contains(factory.Emails.To<SiteVisitReportedEmail>(TestData.AdminEmail), e => e.Code == v.Job.Code);

        // A free item from the visit needs its client price before the price is confirmed.
        var unpriced = await v.Admin.PostAsJsonAsync($"/api/admin/jobs/{v.Job.JobId}/confirm-quote", new { acknowledgeLowMargin = true }, ApiClient.Json);
        await ProblemAssert.HasCodeAsync(unpriced, HttpStatusCode.UnprocessableEntity, "validation_failed");
        var priced = new JsonObject
        {
            ["occupancy"] = draft.GetProperty("occupancy").GetString(),
            ["elements"] = JsonNode.Parse(draft.GetProperty("elements").GetRawText()),
            ["extras"] = new JsonArray(new JsonObject { ["description"] = "Reparar fissura na parede B", ["clientPriceCents"] = 9000, ["painterPayCents"] = 4000 }),
        };
        Assert.Equal(HttpStatusCode.OK, (await v.Admin.PutAsJsonAsync($"/api/admin/jobs/{v.Job.JobId}/draft-quote", priced, ApiClient.Json)).StatusCode);
        var confirmed = await v.Admin.PostAsJsonAsync($"/api/admin/jobs/{v.Job.JobId}/confirm-quote", new { acknowledgeLowMargin = true }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
    }

    [Fact]
    public async Task A2_6_only_a_covering_active_painter_gets_the_visit_and_only_they_see_it()
    {
        var job = await TestJobs.SendAsync(factory);
        var admin = await ApiClient.CreateAdminAsync(factory);
        var (_, _, lisbonLink) = await TestPainterAccounts.CreateAsync(factory, admin, ["Lisboa"]);
        var lisbon = await TestPainterAccounts.SignUpAsync(factory, lisbonLink);

        var refused = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/site-visit",
            new { assigneeId = Guid.Parse(lisbonLink["userId"]), at = DateTimeOffset.UtcNow.AddDays(2) }, ApiClient.Json);
        await ProblemAssert.HasCodeAsync(refused, HttpStatusCode.UnprocessableEntity, "validation_failed");

        var v = await AssignedAsync();
        await ProblemAssert.HasCodeAsync(await lisbon.GetAsync($"/api/painter/visits/{v.Job.JobId}"), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.HasCodeAsync(await v.Job.Client.GetAsync($"/api/painter/visits/{v.Job.JobId}"), HttpStatusCode.Forbidden, "forbidden");
        var bad = new JsonObject { ["elements"] = new JsonArray(), ["notes"] = "x" };
        await ProblemAssert.HasCodeAsync(await v.Painter.PostAsJsonAsync($"/api/painter/visits/{v.Job.JobId}/report", bad, ApiClient.Json),
            HttpStatusCode.UnprocessableEntity, "validation_failed");
    }

    /// <summary>CLAUDE.md rule 8: the visit shows what to measure, never what anyone pays.</summary>
    [Fact]
    public async Task The_visit_never_carries_prices()
    {
        string[] forbidden = ["price", "total", "vat", "deposit", "balance", "margin", "cents", "rate"];
        var v = await AssignedAsync();

        var json = await (await v.Painter.GetAsync($"/api/painter/visits/{v.Job.JobId}")).ReadJsonAsync();

        Assert.DoesNotContain(Names(json), n => forbidden.Any(f => n.Contains(f, StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<string> Names(JsonElement json) => json.ValueKind switch
    {
        JsonValueKind.Object => json.EnumerateObject().SelectMany(p => Names(p.Value).Prepend(p.Name)),
        JsonValueKind.Array => json.EnumerateArray().SelectMany(Names),
        _ => [],
    };
}
