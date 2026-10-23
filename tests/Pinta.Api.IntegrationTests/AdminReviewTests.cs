using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pinta.Modules.Notifications.Contracts;

namespace Pinta.Api.IntegrationTests;

/// <summary>The admin's review (A2), board (A3.1) and transitions (A3.3, A3.4) through the API.</summary>
public class AdminReviewTests(PintaApiFactory factory)
{
    private static async Task<JsonElement> DetailAsync(HttpClient admin, Guid jobId) =>
        await (await admin.GetAsync($"/api/admin/jobs/{jobId}")).ReadJsonAsync();

    /// <summary>The draft body as the admin screen sends it: the current elements plus the given extras.</summary>
    private static async Task<HttpResponseMessage> SaveDraftAsync(HttpClient admin, Guid jobId, object[] extras, long? manualSubtotalCents = null, string? reason = null)
    {
        var quote = (await DetailAsync(admin, jobId)).GetProperty("currentQuote");
        var body = new JsonObject
        {
            ["occupancy"] = quote.GetProperty("occupancy").GetString(),
            ["elements"] = JsonNode.Parse(quote.GetProperty("elements").GetRawText()),
            ["extras"] = JsonSerializer.SerializeToNode(extras, ApiClient.Json),
            ["premium"] = null,
            ["manualSubtotalCents"] = manualSubtotalCents,
            ["adjustmentReason"] = reason,
        };
        return await admin.PutAsJsonAsync($"/api/admin/jobs/{jobId}/draft-quote", body, ApiClient.Json);
    }

    [Fact]
    public async Task A2_TC10_admin_reviews_confirms_and_the_client_sees_the_closed_price_without_internals()
    {
        var job = await TestJobs.SendAsync(factory, TestJobs.Flat, "furnished");
        var admin = await ApiClient.CreateAdminAsync(factory);

        var queue = await (await admin.GetAsync("/api/admin/reviews")).ReadJsonAsync();
        var item = queue.GetProperty("inReview").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == job.JobId);
        Assert.Equal("green", item.GetProperty("light").GetString());
        Assert.Equal(266.44m, item.GetProperty("areaM2").GetDecimal());

        var draft = await SaveDraftAsync(admin, job.JobId, [new { catalogKey = "anti_damp_treatment" }]);
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
        var breakdown = (await draft.ReadJsonAsync()).GetProperty("breakdown");
        Assert.Equal((318468, 66809), (breakdown.GetProperty("totalCents").GetInt64(), breakdown.GetProperty("marginCents").GetInt64()));

        var confirm = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/confirm-quote",
            new { note = "Inclui o tratamento anti-humidade.", acknowledgeLowMargin = false }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        var response = await job.Client.GetAsync($"/api/jobs/{job.JobId}");
        var raw = await response.Content.ReadAsStringAsync();
        foreach (var hidden in new[] { "margin", "painterPay", "painterTotal", "material", "labour", "rate" })
        {
            Assert.DoesNotContain(hidden, raw, StringComparison.OrdinalIgnoreCase);
        }

        var detail = await response.ReadJsonAsync();
        var quote = detail.GetProperty("quote");
        Assert.Equal("quoted", detail.GetProperty("status").GetString());
        Assert.Equal((318468, 95540, 222928), (quote.GetProperty("totalCents").GetInt64(), quote.GetProperty("depositCents").GetInt64(), quote.GetProperty("balanceCents").GetInt64()));
        Assert.Equal(["work", "extra"], quote.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("kind").GetString()));
        Assert.Equal("Inclui o tratamento anti-humidade.", quote.GetProperty("note").GetString());
        // The estimate stays next to the closed price.
        Assert.Equal(266.44m, detail.GetProperty("areaM2").GetDecimal());
        Assert.True(detail.GetProperty("rangeMinCents").GetInt64() > 0);

        var email = Assert.Single(factory.Emails.To<PriceConfirmedEmail>(job.Email));
        Assert.Equal((318468, 95540, job.JobId), (email.TotalCents, email.DepositCents, email.JobId));

        var adminDetail = await DetailAsync(admin, job.JobId);
        Assert.Equal(2, adminDetail.GetProperty("versions").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, adminDetail.GetProperty("draft").ValueKind);
        var last = adminDetail.GetProperty("history").EnumerateArray().Last();
        Assert.Equal(("quoted", "admin"), (last.GetProperty("to").GetString(), last.GetProperty("actor").GetString()));
    }

    [Fact]
    public async Task A2_8_low_margin_needs_explicit_acknowledgment()
    {
        // TC07: anti-damp bathroom, 19.5% margin.
        object[] bathroom = [new { kind = "room", roomTypeId = "bathroom", length = 2.5m, width = 2m, height = 2.4m, walls = new[] { true, true, true, true }, ceiling = true, paintId = "anti_damp", damage = true }];
        var job = await TestJobs.SendAsync(factory, bathroom);
        var admin = await ApiClient.CreateAdminAsync(factory);

        var refused = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/confirm-quote", new { acknowledgeLowMargin = false }, ApiClient.Json);
        var accepted = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/confirm-quote", new { acknowledgeLowMargin = true }, ApiClient.Json);

        var problem = await ProblemAssert.HasCodeAsync(refused, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("acknowledgeLowMargin", out _));
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
    }

    [Fact]
    public async Task A2_7_manual_subtotal_needs_a_reason()
    {
        var job = await TestJobs.SendAsync(factory, TestJobs.Flat, "furnished");
        var admin = await ApiClient.CreateAdminAsync(factory);

        var noReason = await SaveDraftAsync(admin, job.JobId, [new { catalogKey = "anti_damp_treatment" }], 250000);
        var adjusted = await SaveDraftAsync(admin, job.JobId, [new { catalogKey = "anti_damp_treatment" }], 250000, "Cliente habitual");

        await ProblemAssert.HasCodeAsync(noReason, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal(307500, (await adjusted.ReadJsonAsync()).GetProperty("breakdown").GetProperty("totalCents").GetInt64());
    }

    [Fact]
    public async Task A2_9_reject_needs_a_reason_emails_the_client_and_cannot_repeat()
    {
        var job = await TestJobs.SendAsync(factory);
        var admin = await ApiClient.CreateAdminAsync(factory);

        var noReason = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/reject", new { reason = " " }, ApiClient.Json);
        var rejected = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/reject", new { reason = "Fora do nosso âmbito." }, ApiClient.Json);
        var again = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/reject", new { reason = "Outra vez." }, ApiClient.Json);

        await ProblemAssert.HasCodeAsync(noReason, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal(HttpStatusCode.NoContent, rejected.StatusCode);
        await ProblemAssert.HasCodeAsync(again, HttpStatusCode.Conflict, "invalid_transition");
        Assert.Equal("Fora do nosso âmbito.", Assert.Single(factory.Emails.To<RequestRejectedEmail>(job.Email)).Reason);
        var detail = await (await job.Client.GetAsync($"/api/jobs/{job.JobId}")).ReadJsonAsync();
        Assert.Equal("Fora do nosso âmbito.", detail.GetProperty("timeline").EnumerateArray().Last().GetProperty("reason").GetString());
    }

    [Fact]
    public async Task A2_6_site_visit_with_a_partner_then_back_to_review()
    {
        var job = await TestJobs.SendAsync(factory);
        var admin = await ApiClient.CreateAdminAsync(factory);
        var partner = (await (await admin.GetAsync("/api/admin/partners")).ReadJsonAsync()).EnumerateArray().First();
        var at = DateTimeOffset.UtcNow.AddDays(3);

        var past = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/site-visit",
            new { assigneeId = partner.GetProperty("id").GetGuid(), at = DateTimeOffset.UtcNow.AddHours(-1) }, ApiClient.Json);
        var notPartner = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/site-visit", new { assigneeId = Guid.NewGuid(), at }, ApiClient.Json);
        var visit = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/site-visit",
            new { assigneeId = partner.GetProperty("id").GetGuid(), at, priceCents = 3000 }, ApiClient.Json);
        var confirmDuringVisit = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/confirm-quote", new { acknowledgeLowMargin = true }, ApiClient.Json);

        await ProblemAssert.HasCodeAsync(past, HttpStatusCode.UnprocessableEntity, "validation_failed");
        await ProblemAssert.HasCodeAsync(notPartner, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal(HttpStatusCode.NoContent, visit.StatusCode);
        await ProblemAssert.HasCodeAsync(confirmDuringVisit, HttpStatusCode.Conflict, "invalid_transition");
        Assert.Single(factory.Emails.To<SiteVisitScheduledEmail>(job.Email));
        Assert.NotEmpty(factory.Emails.To<SiteVisitScheduledEmail>(partner.GetProperty("email").GetString()!));
        var clientView = await (await job.Client.GetAsync($"/api/jobs/{job.JobId}")).ReadJsonAsync();
        Assert.Equal(3000, clientView.GetProperty("siteVisit").GetProperty("priceCents").GetInt64());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/site-visit/complete", new { }, ApiClient.Json)).StatusCode);
        var draft = await (await SaveDraftAsync(admin, job.JobId, [])).ReadJsonAsync();
        Assert.Equal("site_visit", draft.GetProperty("origin").GetString());
        Assert.Equal("in_review", (await DetailAsync(admin, job.JobId)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task A3_3_A3_4_only_allowed_transitions_with_the_current_version()
    {
        var job = await TestJobs.SendAsync(factory);
        var admin = await ApiClient.CreateAdminAsync(factory);
        var detail = await DetailAsync(admin, job.JobId);
        var version = detail.GetProperty("version").GetUInt32();

        Assert.Equal(["site_visit", "quoted", "rejected", "cancelled"], detail.GetProperty("allowedTransitions").EnumerateArray().Select(s => s.GetString()));
        var notAllowed = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/transition", new { to = "scheduled", reason = "Teste" }, ApiClient.Json);
        var ownAction = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/transition", new { to = "quoted", reason = "Teste" }, ApiClient.Json);
        var stale = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/cancel", new { reason = "Duplicado", fullRefund = false, version = version + 1 }, ApiClient.Json);
        var preview = await (await admin.GetAsync($"/api/admin/jobs/{job.JobId}/cancellation-preview")).ReadJsonAsync();
        var cancelled = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/cancel", new { reason = "Duplicado", fullRefund = false, version }, ApiClient.Json);

        await ProblemAssert.HasCodeAsync(notAllowed, HttpStatusCode.Conflict, "invalid_transition");
        await ProblemAssert.HasCodeAsync(ownAction, HttpStatusCode.UnprocessableEntity, "validation_failed");
        await ProblemAssert.HasCodeAsync(stale, HttpStatusCode.Conflict, "concurrency_conflict");
        Assert.Equal((true, 0), (preview.GetProperty("cancellable").GetBoolean(), preview.GetProperty("refundCents").GetInt64()));
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        await ProblemAssert.HasCodeAsync(
            await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/transition", new { to = "in_review", reason = "Teste" }, ApiClient.Json),
            HttpStatusCode.Conflict, "invalid_transition");
        Assert.Empty((await DetailAsync(admin, job.JobId)).GetProperty("allowedTransitions").EnumerateArray());
    }

    [Fact]
    public async Task A3_1_list_filters_by_status_and_searches_by_code_name_or_email()
    {
        var job = await TestJobs.SendAsync(factory);
        var admin = await ApiClient.CreateAdminAsync(factory);

        var byEmail = await (await admin.GetAsync($"/api/admin/jobs?q={Uri.EscapeDataString(job.Email)}")).ReadJsonAsync();
        var byCode = await (await admin.GetAsync($"/api/admin/jobs?q={job.Code}&status=in_review&status=quoted")).ReadJsonAsync();
        var otherStatus = await (await admin.GetAsync($"/api/admin/jobs?q={job.Code}&status=cancelled")).ReadJsonAsync();
        var badStatus = await admin.GetAsync("/api/admin/jobs?status=InReview");
        var csv = await admin.GetAsync($"/api/admin/jobs/export?q={job.Code}");

        var row = Assert.Single(byEmail.GetProperty("items").EnumerateArray());
        Assert.Equal((job.Code, "Ana Costa", 49225), (row.GetProperty("code").GetString(), row.GetProperty("clientName").GetString(), row.GetProperty("totalCents").GetInt64()));
        Assert.Equal(1, byCode.GetProperty("total").GetInt32());
        Assert.Equal(0, otherStatus.GetProperty("total").GetInt32());
        await ProblemAssert.HasCodeAsync(badStatus, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        var lines = (await csv.Content.ReadAsStringAsync()).TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("code,status,client", lines[0]);
        Assert.StartsWith($"{job.Code},in_review,Ana Costa,{job.Email},Sintra,", lines[1]);
    }

    [Fact]
    public async Task S1_admin_routes_need_the_admin_role_and_jobs_are_only_their_clients()
    {
        var job = await TestJobs.SendAsync(factory);
        var other = await TestJobs.SendAsync(factory);
        var admin = await ApiClient.CreateAdminAsync(factory);

        await ProblemAssert.HasCodeAsync(await job.Client.GetAsync("/api/admin/jobs"), HttpStatusCode.Forbidden, "forbidden");
        await ProblemAssert.HasCodeAsync(await job.Client.GetAsync($"/api/admin/jobs/{job.JobId}"), HttpStatusCode.Forbidden, "forbidden");
        await ProblemAssert.HasCodeAsync(await other.Client.GetAsync($"/api/jobs/{job.JobId}"), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.HasCodeAsync(await other.Client.PostAsJsonAsync($"/api/jobs/{job.JobId}/cancel", new { }, ApiClient.Json), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.HasCodeAsync(await admin.GetAsync($"/api/admin/jobs/{Guid.NewGuid()}"), HttpStatusCode.NotFound, "not_found");
        // Client routes are for clients.
        await ProblemAssert.HasCodeAsync(await admin.GetAsync("/api/jobs"), HttpStatusCode.Forbidden, "forbidden");
    }
}
