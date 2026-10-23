using System.Net;
using System.Net.Http.Json;
using Pinta.Modules.Notifications.Contracts;

namespace Pinta.Api.IntegrationTests;

/// <summary>C5.8: the client's account, password and deletion executed by the admin.</summary>
public class AccountTests(PintaApiFactory factory)
{
    private static object Profile(string email, string name = "Ana Costa", string locale = "pt", string? taxId = null) =>
        new { name, email, phone = "+351912345678", locale, taxId, companyName = taxId is null ? null : "Costa & Filhos, Lda.", marketingConsent = true };

    [Fact]
    public async Task C5_8_the_client_edits_the_account_and_a_new_email_is_confirmed_again()
    {
        var job = await TestJobs.SendAsync(factory);

        var edited = await job.Client.PutAsJsonAsync("/api/me", Profile(job.Email, "Ana Maria Costa", "es", "123456789"), ApiClient.Json);
        var badNif = await job.Client.PutAsJsonAsync("/api/me", Profile(job.Email, taxId: "12345"), ApiClient.Json);
        var taken = await job.Client.PutAsJsonAsync("/api/me", Profile(TestData.AdminEmail), ApiClient.Json);
        var newEmail = $"ana-{Guid.NewGuid():N}@example.test";
        var moved = await job.Client.PutAsJsonAsync("/api/me", Profile(newEmail, "Ana Maria Costa", "es", "123456789"), ApiClient.Json);

        var profile = await edited.ReadJsonAsync();
        Assert.Equal(("Ana Maria Costa", "es", "123456789", true, true), (profile.GetProperty("name").GetString(), profile.GetProperty("locale").GetString(),
            profile.GetProperty("taxId").GetString(), profile.GetProperty("marketingConsent").GetBoolean(), profile.GetProperty("emailConfirmed").GetBoolean()));
        await ProblemAssert.HasCodeAsync(badNif, HttpStatusCode.UnprocessableEntity, "validation_failed");
        await ProblemAssert.HasCodeAsync(taken, HttpStatusCode.Conflict, "email_taken");
        var movedProfile = await moved.ReadJsonAsync();
        Assert.Equal((newEmail, false), (movedProfile.GetProperty("email").GetString(), movedProfile.GetProperty("emailConfirmed").GetBoolean()));
        Assert.Single(factory.Emails.To<ConfirmEmailEmail>(newEmail));
        Assert.Equal("es", (await (await job.Client.GetAsync("/api/auth/me")).ReadJsonAsync()).GetProperty("locale").GetString());
    }

    [Fact]
    public async Task C5_8_changing_the_password_needs_the_current_one()
    {
        var job = await TestJobs.SendAsync(factory);

        var wrong = await job.Client.PostAsJsonAsync("/api/me/password", new { currentPassword = "not-the-password", newPassword = "another-long-password" }, ApiClient.Json);
        var changed = await job.Client.PostAsJsonAsync("/api/me/password", new { currentPassword = TestData.Password, newPassword = "another-long-password" }, ApiClient.Json);

        await ProblemAssert.HasCodeAsync(wrong, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await job.Client.GetAsync("/api/me")).StatusCode);
        var fresh = await ApiClient.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.LoginAsync(job.Email, TestData.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fresh.LoginAsync(job.Email, "another-long-password")).StatusCode);
    }

    [Fact]
    public async Task C5_8_the_admin_executes_the_deletion_cancelling_active_jobs_and_removing_personal_data()
    {
        var job = await TestJobs.SendAsync(factory);
        var admin = await ApiClient.CreateAdminAsync(factory);

        Assert.Equal(HttpStatusCode.NoContent, (await job.Client.PostAsJsonAsync("/api/me/delete-request", new { }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await job.Client.PostAsJsonAsync("/api/me/delete-request", new { }, ApiClient.Json)).StatusCode);
        Assert.Single(factory.Emails.To<AccountDeletionRequestedEmail>(TestData.AdminEmail), e => e.ClientEmail == job.Email);
        var request = (await (await admin.GetAsync("/api/admin/deletion-requests")).ReadJsonAsync()).EnumerateArray()
            .Single(r => r.GetProperty("email").GetString() == job.Email);
        var line = Assert.Single(request.GetProperty("jobs").EnumerateArray());
        Assert.Equal((job.Code, "cancel"), (line.GetProperty("code").GetString(), line.GetProperty("action").GetString()));

        var executed = await admin.PostAsJsonAsync($"/api/admin/deletion-requests/{request.GetProperty("userId").GetGuid()}/execute", new { }, ApiClient.Json);
        var twice = await admin.PostAsJsonAsync($"/api/admin/deletion-requests/{request.GetProperty("userId").GetGuid()}/execute", new { }, ApiClient.Json);

        Assert.Equal(HttpStatusCode.NoContent, executed.StatusCode);
        await ProblemAssert.HasCodeAsync(twice, HttpStatusCode.NotFound, "not_found");
        var detail = await (await admin.GetAsync($"/api/admin/jobs/{job.JobId}")).ReadJsonAsync();
        Assert.Equal(("cancelled", "—", job.Code), (detail.GetProperty("status").GetString(), detail.GetProperty("address").GetProperty("line1").GetString(),
            detail.GetProperty("code").GetString()));
        Assert.DoesNotContain(job.Email, detail.GetRawText());
        Assert.Empty(detail.GetProperty("files").EnumerateArray());
        var fresh = await ApiClient.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.LoginAsync(job.Email, TestData.Password)).StatusCode);
        Assert.DoesNotContain((await (await admin.GetAsync("/api/admin/deletion-requests")).ReadJsonAsync()).EnumerateArray(),
            r => r.GetProperty("userId").GetGuid() == request.GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task C5_8_a_job_in_progress_blocks_the_deletion_and_only_clients_have_an_account_page()
    {
        var s = await TestAssignments.ScheduledAsync(factory);
        await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/start", new { }, ApiClient.Json);
        await s.Job.Client.PostAsJsonAsync("/api/me/delete-request", new { }, ApiClient.Json);
        var request = (await (await s.Admin.GetAsync("/api/admin/deletion-requests")).ReadJsonAsync()).EnumerateArray()
            .Single(r => r.GetProperty("email").GetString() == s.Job.Email);

        Assert.Equal("blocked", Assert.Single(request.GetProperty("jobs").EnumerateArray()).GetProperty("action").GetString());
        await ProblemAssert.HasCodeAsync(await s.Admin.PostAsJsonAsync($"/api/admin/deletion-requests/{request.GetProperty("userId").GetGuid()}/execute", new { }, ApiClient.Json),
            HttpStatusCode.Conflict, "invalid_transition");
        await ProblemAssert.HasCodeAsync(await s.Painter.GetAsync("/api/me"), HttpStatusCode.Forbidden, "forbidden");
        await ProblemAssert.HasCodeAsync(await s.Job.Client.GetAsync("/api/admin/deletion-requests"), HttpStatusCode.Forbidden, "forbidden");
    }
}
