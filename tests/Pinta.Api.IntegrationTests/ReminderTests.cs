using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Features.Expiry;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Painters.Features;

namespace Pinta.Api.IntegrationTests;

/// <summary>S3: the daily and weekly reminders of the background jobs.</summary>
public class ReminderTests(PintaApiFactory factory)
{
    private async Task SqlAsync(FormattableString sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<JobsDbContext>().Database.ExecuteSqlInterpolatedAsync(sql);
    }

    private async Task RunAsync<TJob>(Func<TJob, Task<int>> run) where TJob : notnull
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await run(scope.ServiceProvider.GetRequiredService<TJob>());
    }

    [Fact]
    public async Task S3_the_client_is_reminded_once_three_days_before_the_price_expires()
    {
        var job = await TestJobs.SendAsync(factory);
        await TestReview.ConfirmAsync(factory, job.JobId);
        var inThreeDays = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Pinta.SharedKernel.Time.BusinessDays.Lisbon).DateTime).AddDays(3);
        await SqlAsync($"UPDATE jobs.jobs SET quote_valid_until = {inThreeDays} WHERE id = {job.JobId}");

        await RunAsync<QuoteExpiryReminders>(r => r.RunAsync());
        await RunAsync<QuoteExpiryReminders>(r => r.RunAsync());

        var email = Assert.Single(factory.Emails.To<PriceExpiringEmail>(job.Email));
        Assert.Equal((job.Code, inThreeDays, 14768L), (email.Code, email.ValidUntil, email.DepositCents));
    }

    [Fact]
    public async Task S3_the_admins_hear_about_requests_past_their_review_deadline()
    {
        var job = await TestJobs.SendAsync(factory);
        await SqlAsync($"UPDATE jobs.jobs SET review_due_on = {DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2)} WHERE id = {job.JobId}");

        await RunAsync<OverdueReviewAlerts>(r => r.RunAsync());

        Assert.Contains(factory.Emails.To<OverdueReviewsEmail>(TestData.AdminEmail), e => e.Codes.Contains(job.Code));
    }

    [Fact]
    public async Task S3_the_admins_hear_on_mondays_about_painter_documents_expiring_within_30_days()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var (painterId, _, _) = await TestPainterAccounts.CreateAsync(factory, admin);
        var content = new byte[1024];
        var upload = await (await admin.PostAsJsonAsync($"/api/admin/painters/{painterId}/documents/upload-url",
            new { contentType = "application/pdf", sizeBytes = content.Length }, ApiClient.Json)).ReadJsonAsync();
        Assert.True((await MediaTests.UploadToStorageAsync(upload, content, "application/pdf")).IsSuccessStatusCode);
        var expiresOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);
        (await admin.PostAsJsonAsync($"/api/admin/painters/{painterId}/documents",
            new { fileId = upload.GetProperty("fileId").GetGuid(), kind = "insurance", expiresOn }, ApiClient.Json)).EnsureSuccessStatusCode();

        await RunAsync<PainterDocumentAlerts>(r => r.RunAsync());

        Assert.Contains(factory.Emails.To<PainterDocumentsExpiringEmail>(TestData.AdminEmail),
            e => e.Lines.Any(l => l.Contains($"{expiresOn:yyyy-MM-dd}") && l.Contains("Insurance")));
    }
}
