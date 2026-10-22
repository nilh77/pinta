using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pinta.Modules.Media.Data;
using Pinta.Modules.Notifications.Contracts;

namespace Pinta.Api.IntegrationTests;

public class JobSubmissionTests(PintaApiFactory factory)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>TC01 (bedroom 4 × 3, empty) sent from a covered postal code in Sintra.</summary>
    private static Dictionary<string, object?> Request(Guid? draftId = null, string postalCode = "2710-123", object? dates = null, object? billing = null) => new()
    {
        ["draftId"] = draftId ?? Guid.NewGuid(),
        ["occupancy"] = "empty",
        ["elements"] = new[]
        {
            new
            {
                kind = "room", name = "Quarto", roomTypeId = "bedroom", length = 4m, width = 3m, height = 2.6m,
                walls = new[] { true, true, true, true }, ceiling = true, paintId = "matte", damage = false, clientKey = "room-1",
            },
        },
        ["description"] = "Paredes brancas.",
        ["address"] = new { line1 = "Rua das Flores, 24", floor = "2.º Esq.", postalCode, locality = "Sintra", accessNotes = "Elevador" },
        ["preferredDates"] = dates ?? new { asap = true },
        ["billing"] = billing,
    };

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, object body, Guid? key = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/jobs") { Content = JsonContent.Create(body, options: ApiClient.Json) };
        message.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        return await client.SendAsync(message);
    }

    private async Task<(HttpClient Client, string Email)> NewClientAsync()
    {
        var email = TestData.UniqueEmail();
        var client = await ApiClient.CreateAsync(factory);
        (await client.RegisterAsync(email, sendConfirmationEmail: false)).EnsureSuccessStatusCode();
        return (client, email);
    }

    [Fact]
    public async Task C4_4_C4_5_request_waits_for_the_email_and_goes_to_review_once_confirmed()
    {
        var (client, email) = await NewClientAsync();
        Assert.Empty(factory.Emails.To<ConfirmEmailEmail>(email));

        var response = await SubmitAsync(client, Request());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadJsonAsync();
        Assert.Matches(@"^PT-\d{4}-\d{4}$", created.GetProperty("code").GetString());
        Assert.Equal("pending_email", created.GetProperty("status").GetString());
        Assert.Equal($"/api/jobs/{created.GetProperty("id").GetString()}", response.Headers.Location?.OriginalString);

        var job = await (await client.GetAsync(response.Headers.Location)).ReadJsonAsync();
        Assert.Equal(48.40m, job.GetProperty("areaM2").GetDecimal());
        Assert.Equal(44000, job.GetProperty("rangeMinCents").GetInt64());
        Assert.Equal(57000, job.GetProperty("rangeMaxCents").GetInt64());
        Assert.Equal(JsonValueKind.Null, job.GetProperty("reviewDueOn").ValueKind);

        // One email: the request with the confirmation link (docs/integrations.md).
        var mail = Assert.Single(factory.Emails.To<RequestReceivedEmail>(email));
        Assert.Equal(created.GetProperty("code").GetString(), mail.Code);
        Assert.NotNull(mail.ConfirmationUrl);
        var query = QueryHelpers.ParseQuery(new Uri(mail.ConfirmationUrl!).Query);
        var confirmed = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new { userId = query["userId"].ToString(), token = query["token"].ToString() }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);

        var reviewed = await (await client.GetAsync(response.Headers.Location)).ReadJsonAsync();
        Assert.Equal("in_review", reviewed.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, reviewed.GetProperty("reviewDueOn").ValueKind);
    }

    [Fact]
    public async Task C4_4_a_client_with_a_confirmed_email_goes_straight_to_review()
    {
        var (client, email) = await NewClientAsync();
        await SubmitAsync(client, Request());
        var link = new Uri(factory.Emails.To<RequestReceivedEmail>(email)[0].ConfirmationUrl!);
        var query = QueryHelpers.ParseQuery(link.Query);
        await client.PostAsJsonAsync("/api/auth/confirm-email", new { userId = query["userId"].ToString(), token = query["token"].ToString() }, ApiClient.Json);

        var second = await (await SubmitAsync(client, Request())).ReadJsonAsync();

        Assert.Equal("in_review", second.GetProperty("status").GetString());
        Assert.Null(factory.Emails.To<RequestReceivedEmail>(email)[1].ConfirmationUrl);
    }

    [Fact]
    public async Task C4_4_retrying_with_the_same_key_creates_one_job()
    {
        var (client, _) = await NewClientAsync();
        var key = Guid.NewGuid();
        var body = Request();

        var first = await SubmitAsync(client, body, key);
        var second = await SubmitAsync(client, body, key);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal((await first.ReadJsonAsync()).GetProperty("id").GetString(), (await second.ReadJsonAsync()).GetProperty("id").GetString());
        Assert.Equal(first.Headers.Location, second.Headers.Location);

        body["description"] = "Outra coisa";
        await ProblemAssert.HasCodeAsync(await SubmitAsync(client, body, key), HttpStatusCode.Conflict, "idempotency_conflict");

        using var withoutKey = new HttpRequestMessage(HttpMethod.Post, "/api/jobs") { Content = JsonContent.Create(Request(), options: ApiClient.Json) };
        await ProblemAssert.HasCodeAsync(await client.SendAsync(withoutKey), HttpStatusCode.BadRequest, "idempotency_key_required");
    }

    [Fact]
    public async Task C4_1_a_postal_code_outside_the_area_is_rejected()
    {
        var (client, _) = await NewClientAsync();

        var problem = await ProblemAssert.HasCodeAsync(await SubmitAsync(client, Request(postalCode: "4000-123")), HttpStatusCode.UnprocessableEntity, "out_of_coverage");

        Assert.True(problem.GetProperty("errors").TryGetProperty("address.postalCode", out _));
    }

    [Fact]
    public async Task C4_2_dates_start_after_the_notice_and_last_at_most_60_days()
    {
        var (client, _) = await NewClientAsync();

        var tooSoon = await SubmitAsync(client, Request(dates: new { asap = false, from = Today, to = Today.AddDays(5) }));
        var problem = await ProblemAssert.HasCodeAsync(tooSoon, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("preferredDates.from", out _));

        var from = Today.AddDays(14);
        var tooLong = await SubmitAsync(client, Request(dates: new { asap = false, from, to = from.AddDays(61) }));
        problem = await ProblemAssert.HasCodeAsync(tooLong, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("preferredDates.to", out _));

        Assert.Equal(HttpStatusCode.Created, (await SubmitAsync(client, Request(dates: new { asap = false, from, to = from.AddDays(60) }))).StatusCode);
    }

    [Fact]
    public async Task C4_3_company_invoice_needs_a_valid_nif_and_a_name()
    {
        var (client, _) = await NewClientAsync();

        var invalid = await SubmitAsync(client, Request(billing: new { taxId = "123456780", companyName = "" }));

        var problem = await ProblemAssert.HasCodeAsync(invalid, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("billing.taxId", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("billing.companyName", out _));
        Assert.Equal(HttpStatusCode.Created, (await SubmitAsync(client, Request(billing: new { taxId = "123456789", companyName = "Ana Lda." }))).StatusCode);
    }

    [Fact]
    public async Task C4_4_invalid_elements_report_the_same_errors_as_the_estimate()
    {
        var (client, _) = await NewClientAsync();
        var body = Request();
        body["elements"] = new[] { new { kind = "room", roomTypeId = "bedroom", length = 4m, width = 3m, walls = new[] { false, false, false, false }, ceiling = false, paintId = "matte", damage = false } };

        var problem = await ProblemAssert.HasCodeAsync(await SubmitAsync(client, body), HttpStatusCode.UnprocessableEntity, "validation_failed");

        Assert.True(problem.GetProperty("errors").TryGetProperty("elements[0].walls", out _));
    }

    [Fact]
    public async Task C4_4_only_clients_send_requests_and_only_the_owner_sees_them()
    {
        var anonymous = await ApiClient.CreateAsync(factory);
        await ProblemAssert.HasCodeAsync(await SubmitAsync(anonymous, Request()), HttpStatusCode.Unauthorized, "unauthenticated");
        var admin = await ApiClient.CreateAdminAsync(factory);
        await ProblemAssert.HasCodeAsync(await SubmitAsync(admin, Request()), HttpStatusCode.Forbidden, "forbidden");

        var (owner, _) = await NewClientAsync();
        var location = (await SubmitAsync(owner, Request())).Headers.Location;
        var (other, _) = await NewClientAsync();
        await ProblemAssert.HasCodeAsync(await other.GetAsync(location), HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task C3_1_C4_4_confirmed_draft_photos_are_attached_to_the_job()
    {
        var uploader = await ApiClient.CreateAsync(factory);
        var draftId = Guid.NewGuid();
        await MediaTests.UploadConfirmedAsync(uploader, draftId, "room-1");
        await MediaTests.UploadConfirmedAsync(uploader, draftId, "room-1", "image/png");
        await MediaTests.UploadConfirmedAsync(uploader, draftId, null);
        await MediaTests.UploadConfirmedAsync(uploader, draftId, "deleted-room");
        // Asked for but never uploaded: stays out.
        await uploader.PostAsJsonAsync("/api/media/upload-url", new { draftId, elementKey = "room-1", kind = "video", contentType = "video/mp4", sizeBytes = 80L * 1024 * 1024 }, ApiClient.Json);

        var (client, _) = await NewClientAsync();
        var jobId = (await (await SubmitAsync(client, Request(draftId))).ReadJsonAsync()).GetProperty("id").GetGuid();

        await using var scope = factory.Services.CreateAsyncScope();
        var files = await scope.ServiceProvider.GetRequiredService<MediaDbContext>().Files.AsNoTracking().Where(f => f.DraftId == draftId).ToListAsync();
        Assert.Equal(3, files.Count(f => f.JobId == jobId));
        Assert.Equal(2, files.Count(f => f.JobId == jobId && f.QuoteElementId != null));
        Assert.Equal(2, files.Count(f => f.JobId == null));
    }
}
