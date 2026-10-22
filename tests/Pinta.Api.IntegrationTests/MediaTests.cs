using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pinta.Api.IntegrationTests;

public class MediaTests(PintaApiFactory factory)
{
    private static object UploadRequest(Guid draftId, string? elementKey = "room-1", string kind = "image", string contentType = "image/jpeg", long size = 1024) =>
        new { draftId, elementKey, kind, contentType, sizeBytes = size };

    /// <summary>Uploads like the browser: multipart POST of the signed fields and the file last.</summary>
    public static async Task<HttpResponseMessage> UploadToStorageAsync(
        JsonElement upload, byte[] content, string contentType, IReadOnlyDictionary<string, string>? overrides = null)
    {
        using var form = new MultipartFormDataContent();
        foreach (var field in upload.GetProperty("fields").EnumerateObject())
        {
            var value = overrides?.GetValueOrDefault(field.Name) ?? field.Value.GetString()!;
            form.Add(new StringContent(value), field.Name);
        }

        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new(contentType);
        form.Add(file, "file", "photo");
        using var http = new HttpClient();
        return await http.PostAsync(upload.GetProperty("url").GetString(), form);
    }

    public static async Task<Guid> UploadConfirmedAsync(HttpClient client, Guid draftId, string? elementKey, string contentType = "image/jpeg", string kind = "image")
    {
        var content = new byte[2048];
        var upload = await (await client.PostAsJsonAsync("/api/media/upload-url", UploadRequest(draftId, elementKey, kind, contentType, content.Length), ApiClient.Json)).ReadJsonAsync();
        var stored = await UploadToStorageAsync(upload, content, contentType);
        Assert.True(stored.IsSuccessStatusCode, await stored.Content.ReadAsStringAsync());
        var fileId = upload.GetProperty("fileId").GetGuid();
        var confirmed = await client.PostAsJsonAsync($"/api/media/{fileId}/confirm", new { draftId }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        return fileId;
    }

    [Fact]
    public async Task C3_1_anonymous_client_uploads_directly_to_storage_and_confirms()
    {
        var client = await ApiClient.CreateAsync(factory);
        var draftId = Guid.NewGuid();

        var fileId = await UploadConfirmedAsync(client, draftId, "room-1");

        var again = await (await client.PostAsJsonAsync($"/api/media/{fileId}/confirm", new { draftId }, ApiClient.Json)).ReadJsonAsync();
        Assert.True(again.GetProperty("confirmed").GetBoolean());
        Assert.Equal(2048, again.GetProperty("sizeBytes").GetInt64());
    }

    [Fact]
    public async Task C3_1_storage_rejects_uploads_outside_the_signed_policy()
    {
        var client = await ApiClient.CreateAsync(factory);
        var upload = await (await client.PostAsJsonAsync("/api/media/upload-url", UploadRequest(Guid.NewGuid(), size: 100), ApiClient.Json)).ReadJsonAsync();

        var otherType = await UploadToStorageAsync(upload, new byte[100], "image/png", new Dictionary<string, string> { ["Content-Type"] = "image/png" });
        var otherKey = await UploadToStorageAsync(upload, new byte[100], "image/jpeg", new Dictionary<string, string> { ["key"] = "uploads/elsewhere.jpg" });
        var tooBig = await UploadToStorageAsync(upload, new byte[10 * 1024 * 1024 + 1], "image/jpeg");

        Assert.Equal(HttpStatusCode.Forbidden, otherType.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherKey.StatusCode);
        Assert.False(tooBig.IsSuccessStatusCode);
    }

    [Fact]
    public async Task C3_1_confirming_before_uploading_fails_and_another_draft_cannot_see_the_file()
    {
        var client = await ApiClient.CreateAsync(factory);
        var draftId = Guid.NewGuid();
        var upload = await (await client.PostAsJsonAsync("/api/media/upload-url", UploadRequest(draftId), ApiClient.Json)).ReadJsonAsync();
        var fileId = upload.GetProperty("fileId").GetGuid();

        await ProblemAssert.HasCodeAsync(
            await client.PostAsJsonAsync($"/api/media/{fileId}/confirm", new { draftId }, ApiClient.Json),
            HttpStatusCode.UnprocessableEntity, "validation_failed");
        await ProblemAssert.HasCodeAsync(
            await client.PostAsJsonAsync($"/api/media/{fileId}/confirm", new { draftId = Guid.NewGuid() }, ApiClient.Json),
            HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.HasCodeAsync(
            await client.DeleteAsync($"/api/media/{fileId}?draftId={Guid.NewGuid()}"),
            HttpStatusCode.NotFound, "not_found");
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/media/{fileId}?draftId={draftId}")).StatusCode);
    }

    [Fact]
    public async Task C3_1_rejects_unsupported_types_and_oversized_files()
    {
        var client = await ApiClient.CreateAsync(factory);
        var draftId = Guid.NewGuid();

        await ProblemAssert.HasCodeAsync(
            await client.PostAsJsonAsync("/api/media/upload-url", UploadRequest(draftId, contentType: "image/gif"), ApiClient.Json),
            HttpStatusCode.UnsupportedMediaType, "unsupported_media_type");
        await ProblemAssert.HasCodeAsync(
            await client.PostAsJsonAsync("/api/media/upload-url", UploadRequest(draftId, kind: "video", contentType: "image/jpeg"), ApiClient.Json),
            HttpStatusCode.UnsupportedMediaType, "unsupported_media_type");
        await ProblemAssert.HasCodeAsync(
            await client.PostAsJsonAsync("/api/media/upload-url", UploadRequest(draftId, size: 10 * 1024 * 1024 + 1), ApiClient.Json),
            HttpStatusCode.RequestEntityTooLarge, "file_too_large");
        await ProblemAssert.HasCodeAsync(
            await client.PostAsJsonAsync("/api/media/upload-url", UploadRequest(draftId, kind: "video", contentType: "video/mp4", size: 100L * 1024 * 1024 + 1), ApiClient.Json),
            HttpStatusCode.RequestEntityTooLarge, "file_too_large");

        // An 80 MB video is allowed (exit criterion of H3).
        var video = await client.PostAsJsonAsync("/api/media/upload-url", UploadRequest(draftId, kind: "video", contentType: "video/mp4", size: 80L * 1024 * 1024), ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, video.StatusCode);
    }

    [Fact]
    public async Task C3_1_allows_10_photos_and_2_videos_per_element()
    {
        var client = await ApiClient.CreateAsync(factory);
        var draftId = Guid.NewGuid();

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/media/upload-url", UploadRequest(draftId), ApiClient.Json)).StatusCode);
        }

        await ProblemAssert.HasCodeAsync(
            await client.PostAsJsonAsync("/api/media/upload-url", UploadRequest(draftId), ApiClient.Json),
            HttpStatusCode.UnprocessableEntity, "media_limit_reached");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/media/upload-url", UploadRequest(draftId, "room-2"), ApiClient.Json)).StatusCode);

        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/media/upload-url", UploadRequest(draftId, kind: "video", contentType: "video/quicktime"), ApiClient.Json)).StatusCode);
        }

        await ProblemAssert.HasCodeAsync(
            await client.PostAsJsonAsync("/api/media/upload-url", UploadRequest(draftId, kind: "video", contentType: "video/mp4"), ApiClient.Json),
            HttpStatusCode.UnprocessableEntity, "media_limit_reached");
    }
}
