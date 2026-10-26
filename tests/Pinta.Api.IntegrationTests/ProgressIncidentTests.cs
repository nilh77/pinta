using System.Net;
using System.Net.Http.Json;
using Pinta.Modules.Notifications.Contracts;

namespace Pinta.Api.IntegrationTests;

/// <summary>P2: progress photos the client sees and incidents only the admins see.</summary>
public class ProgressIncidentTests(PintaApiFactory factory)
{
    /// <summary>A signed upload of the given context and the file put in storage; null when the API refuses it.</summary>
    private static async Task<(HttpResponseMessage Response, Guid? FileId)> UploadAsync(HttpClient painter, Guid jobId, string context)
    {
        var content = new byte[2048];
        var response = await painter.PostAsJsonAsync($"/api/painter/jobs/{jobId}/media/upload-url",
            new { contentType = "image/jpeg", sizeBytes = content.Length, context }, ApiClient.Json);
        if (!response.IsSuccessStatusCode)
        {
            return (response, null);
        }

        var upload = await response.ReadJsonAsync();
        Assert.True((await MediaTests.UploadToStorageAsync(upload, content, "image/jpeg")).IsSuccessStatusCode);
        return (response, upload.GetProperty("fileId").GetGuid());
    }

    [Fact]
    public async Task P2_progress_photos_while_in_progress_reach_the_client()
    {
        var s = await TestAssignments.ScheduledAsync(factory);
        await ProblemAssert.HasCodeAsync((await UploadAsync(s.Painter, s.Job.JobId, "progress")).Response, HttpStatusCode.Conflict, "invalid_transition");
        await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/start", new { }, ApiClient.Json);

        var photo = (await UploadAsync(s.Painter, s.Job.JobId, "progress")).FileId!.Value;
        var none = await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/progress", new { fileIds = new[] { Guid.NewGuid() } }, ApiClient.Json);
        var added = await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/progress", new { fileIds = new[] { photo } }, ApiClient.Json);

        await ProblemAssert.HasCodeAsync(none, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
        var client = await (await s.Job.Client.GetAsync($"/api/jobs/{s.Job.JobId}")).ReadJsonAsync();
        Assert.Equal(photo, Assert.Single(client.GetProperty("progressPhotos").EnumerateArray()).GetProperty("id").GetGuid());
        var painter = await (await s.Painter.GetAsync($"/api/painter/jobs/{s.Job.JobId}")).ReadJsonAsync();
        Assert.Equal((1, 0), (painter.GetProperty("progressFiles").GetArrayLength(), painter.GetProperty("clientFiles").GetArrayLength()));
    }

    [Fact]
    public async Task P2_an_incident_reaches_the_admins_and_never_the_client()
    {
        var s = await TestAssignments.ScheduledAsync(factory, inDays: 2);
        var photo = (await UploadAsync(s.Painter, s.Job.JobId, "incident")).FileId!.Value;

        var empty = await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/incidents", new { text = "", fileIds = new[] { photo } }, ApiClient.Json);
        var reported = await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/incidents",
            new { text = "Humidade na parede B, precisa de tratamento.", fileIds = new[] { photo } }, ApiClient.Json);

        await ProblemAssert.HasCodeAsync(empty, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal(HttpStatusCode.NoContent, reported.StatusCode);
        Assert.Contains(factory.Emails.To<IncidentReportedEmail>(TestData.AdminEmail), e => e.Code == s.Job.Code && e.Text.StartsWith("Humidade"));
        var admin = await (await s.Admin.GetAsync($"/api/admin/jobs/{s.Job.JobId}")).ReadJsonAsync();
        var incident = Assert.Single(admin.GetProperty("incidents").EnumerateArray());
        Assert.Equal(photo, Assert.Single(incident.GetProperty("files").EnumerateArray()).GetProperty("id").GetGuid());
        var client = await (await s.Job.Client.GetAsync($"/api/jobs/{s.Job.JobId}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Humidade", client);
        Assert.DoesNotContain(photo.ToString(), client);
        var painter = await (await s.Painter.GetAsync($"/api/painter/jobs/{s.Job.JobId}")).ReadJsonAsync();
        Assert.Single(painter.GetProperty("incidents").EnumerateArray());
    }
}
