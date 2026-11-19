using System.Net;
using System.Net.Http.Json;

namespace Pinta.Api.IntegrationTests;

public class JobProtocolTests(PintaApiFactory factory)
{
    [Fact]
    public async Task Accepting_an_offer_creates_a_pdf_for_the_assigned_painter_and_admin()
    {
        var setup = await TestAssignments.ReadyAsync(factory);
        var id = setup.Job.JobId;
        var painterPath = $"/api/painter/jobs/{id}/protocol";
        var adminPath = $"/api/admin/jobs/{id}/protocol";
        (await TestAssignments.OfferAsync(setup.Admin, id, setup.PainterId)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await setup.Admin.GetAsync($"{adminPath}/pdf")).StatusCode);

        var offerId = await TestAssignments.OfferIdAsync(setup.Painter);
        (await setup.Painter.PostAsJsonAsync($"/api/painter/assignments/{offerId}/accept", new { }, ApiClient.Json)).EnsureSuccessStatusCode();

        var painterPdf = await setup.Painter.GetAsync($"{painterPath}/pdf");
        var adminPdf = await setup.Admin.GetAsync($"{adminPath}/pdf");
        Assert.Equal(HttpStatusCode.OK, painterPdf.StatusCode);
        Assert.Equal("application/pdf", painterPdf.Content.Headers.ContentType?.MediaType);
        Assert.Contains(setup.Job.Code, painterPdf.Content.Headers.ContentDisposition?.FileName);
        var original = await painterPdf.Content.ReadAsByteArrayAsync();
        Assert.True(original.AsSpan().StartsWith("%PDF-"u8));
        Assert.Equal(original, await adminPdf.Content.ReadAsByteArrayAsync());
        Assert.Equal("pending", (await (await setup.Painter.GetAsync(painterPath)).ReadJsonAsync()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.Job.Client.GetAsync($"{painterPath}/pdf")).StatusCode);

        var (_, _, otherLink) = await TestPainterAccounts.CreateAsync(factory, setup.Admin);
        var other = await TestPainterAccounts.SignUpAsync(factory, otherLink);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{painterPath}/pdf")).StatusCode);
    }

    [Fact]
    public async Task Finishing_does_not_require_a_protocol_and_an_admin_can_record_the_paper_once()
    {
        var setup = await TestAssignments.ScheduledAsync(factory);
        var id = setup.Job.JobId;
        var adminPath = $"/api/admin/jobs/{id}/protocol";
        var painterPath = $"/api/painter/jobs/{id}";
        Assert.Equal(HttpStatusCode.Conflict, (await setup.Admin.PostAsJsonAsync($"{adminPath}/receive-in-person", new { }, ApiClient.Json)).StatusCode);
        (await setup.Painter.PostAsJsonAsync($"{painterPath}/start", new { }, ApiClient.Json)).EnsureSuccessStatusCode();
        var photos = await PainterJobTests.UploadResultsAsync(setup.Painter, id, 3);
        (await setup.Painter.PostAsJsonAsync($"{painterPath}/finish", new { fileIds = photos }, ApiClient.Json)).EnsureSuccessStatusCode();

        var first = await setup.Admin.PostAsJsonAsync($"{adminPath}/receive-in-person", new { }, ApiClient.Json);
        var retry = await setup.Admin.PostAsJsonAsync($"{adminPath}/receive-in-person", new { }, ApiClient.Json);
        var received = await first.ReadJsonAsync();
        Assert.Equal("received_in_person", received.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(received.GetProperty("receivedBy").GetString()));
        Assert.Equal(received.GetProperty("receivedAt").GetDateTimeOffset(),
            (await retry.ReadJsonAsync()).GetProperty("receivedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task The_painter_can_upload_the_signed_protocol_and_only_the_team_can_read_it()
    {
        var setup = await TestAssignments.ScheduledAsync(factory);
        var id = setup.Job.JobId;
        var painterPath = $"/api/painter/jobs/{id}";
        var protocol = $"{painterPath}/protocol";
        (await setup.Painter.PostAsJsonAsync($"{painterPath}/start", new { }, ApiClient.Json)).EnsureSuccessStatusCode();
        var photos = await PainterJobTests.UploadResultsAsync(setup.Painter, id, 3);
        (await setup.Painter.PostAsJsonAsync($"{painterPath}/finish", new { fileIds = photos }, ApiClient.Json)).EnsureSuccessStatusCode();

        var pdf = await (await setup.Painter.GetAsync($"{protocol}/pdf")).Content.ReadAsByteArrayAsync();
        var uploadResponse = await setup.Painter.PostAsJsonAsync($"{protocol}/upload-url",
            new { contentType = "application/pdf", sizeBytes = pdf.Length }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        var upload = await uploadResponse.ReadJsonAsync();
        (await MediaTests.UploadToStorageAsync(upload, pdf, "application/pdf")).EnsureSuccessStatusCode();
        var fileId = upload.GetProperty("fileId").GetGuid();
        var invalid = await setup.Painter.PostAsJsonAsync($"{protocol}/submit", new { fileIds = new[] { fileId, photos[0] } }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        var submission = await setup.Painter.PostAsJsonAsync($"{protocol}/submit", new { fileIds = new[] { fileId } }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, submission.StatusCode);
        var adminStatus = await (await setup.Admin.GetAsync($"/api/admin/jobs/{id}/protocol")).ReadJsonAsync();
        Assert.Equal("uploaded", adminStatus.GetProperty("status").GetString());
        Assert.Equal(fileId, Assert.Single(adminStatus.GetProperty("files").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await setup.Job.Client.GetAsync($"/api/media/{fileId}/url")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await setup.Admin.PostAsJsonAsync($"/api/admin/jobs/{id}/protocol/receive-in-person", new { }, ApiClient.Json)).StatusCode);
    }

    [Fact]
    public async Task Concurrent_accepts_create_one_protocol_and_keep_the_assignment_conflict_response()
    {
        var setup = await TestAssignments.ReadyAsync(factory);
        var id = setup.Job.JobId;
        (await TestAssignments.OfferAsync(setup.Admin, id, setup.PainterId)).EnsureSuccessStatusCode();
        var offerId = await TestAssignments.OfferIdAsync(setup.Painter);
        var accept = $"/api/painter/assignments/{offerId}/accept";

        var responses = await Task.WhenAll(
            setup.Painter.PostAsJsonAsync(accept, new { }, ApiClient.Json),
            setup.Painter.PostAsJsonAsync(accept, new { }, ApiClient.Json));

        Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }, responses.Select(r => r.StatusCode).Order());
        Assert.Equal(HttpStatusCode.OK, (await setup.Admin.GetAsync($"/api/admin/jobs/{id}/protocol/pdf")).StatusCode);
    }
}
