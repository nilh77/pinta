using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Notifications.Data;

namespace Pinta.Api.IntegrationTests;

/// <summary>Who gets which notice on job events (docs/integrations.md, «Avisos y notificaciones push»).</summary>
public class JobNoticeTests(PintaApiFactory factory)
{
    private sealed record Sent(Guid UserId, NotificationKind Kind, NoticeAudience Audience, string? Name);

    private async Task<List<Sent>> NoticesAsync(string jobCode)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Notifications.AsNoTracking()
            .Where(n => n.JobCode == jobCode).ToListAsync();
        return [.. rows.Select(n => new Sent(n.UserId, n.Kind, n.Audience, n.Data.GetValueOrDefault("name")))];
    }

    /// <summary>The seeded admins; other tests may add admins later, who did not exist when the notice was raised.</summary>
    private async Task<IReadOnlyList<Guid>> AdminIdsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return [.. (await scope.ServiceProvider.GetRequiredService<IUserDirectory>().ListAdminsAsync())
            .Where(a => a.Email.EndsWith("@demo.pinta.pt", StringComparison.Ordinal)).Select(a => a.Id)];
    }

    private static async Task<Guid> UserIdAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/auth/me")).ReadJsonAsync()).GetProperty("id").GetGuid();

    [Fact]
    public async Task A2_1_C5_3_a_new_request_and_a_paid_deposit_reach_every_admin_and_the_client()
    {
        var (job, _) = await TestPayments.PaidDepositAsync(factory);
        var admins = await AdminIdsAsync();
        var client = await UserIdAsync(job.Client);

        var notices = await NoticesAsync(job.Code);

        Assert.NotEmpty(admins);
        Assert.All(admins, admin => Assert.Contains(notices, n =>
            n.UserId == admin && n.Kind == NotificationKind.RequestReceived && n.Audience == NoticeAudience.Admin && n.Name is not null));
        Assert.All(admins, admin => Assert.Contains(notices, n => n.UserId == admin && n.Kind == NotificationKind.DepositPaid));
        Assert.Contains(notices, n => n.UserId == client && n.Kind == NotificationKind.QuoteConfirmed && n.Audience == NoticeAudience.Client);
        Assert.Contains(notices, n => n.UserId == client && n.Kind == NotificationKind.PaymentReceived);
        Assert.DoesNotContain(notices, n => n.UserId == client && n.Audience == NoticeAudience.Admin);
    }

    [Fact]
    public async Task P1_2_offers_reach_the_painter_and_answers_reach_the_admins()
    {
        var s = await TestAssignments.ReadyAsync(factory);
        var (otherId, _, otherLink) = await TestPainterAccounts.CreateAsync(factory, s.Admin);
        var other = await TestPainterAccounts.SignUpAsync(factory, otherLink);
        var painterUser = await UserIdAsync(s.Painter);
        var otherUser = await UserIdAsync(other);

        await TestAssignments.OfferAsync(s.Admin, s.Job.JobId, s.PainterId, 2);
        await s.Admin.PostAsJsonAsync($"/api/admin/jobs/{s.Job.JobId}/assignments/{await TestAssignments.OfferIdAsync(s.Painter)}/withdraw", new { }, ApiClient.Json);
        await TestAssignments.OfferAsync(s.Admin, s.Job.JobId, otherId, 2);
        await other.PostAsJsonAsync($"/api/painter/assignments/{await TestAssignments.OfferIdAsync(other)}/decline", new { reason = "Estou de férias." }, ApiClient.Json);

        var notices = await NoticesAsync(s.Job.Code);
        Assert.Contains(notices, n => n.UserId == painterUser && n.Kind == NotificationKind.OfferReceived && n.Audience == NoticeAudience.Painter);
        Assert.Contains(notices, n => n.UserId == painterUser && n.Kind == NotificationKind.OfferWithdrawn);
        Assert.Contains(notices, n => n.UserId == otherUser && n.Kind == NotificationKind.OfferReceived);
        Assert.Contains(notices, n => n.Kind == NotificationKind.OfferDeclined && n.Audience == NoticeAudience.Admin && n.Name is not null);
    }

    [Fact]
    public async Task P2_acceptance_start_and_progress_photos_reach_the_client_and_the_admins()
    {
        var s = await TestAssignments.ScheduledAsync(factory);
        await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/start", new { }, ApiClient.Json);
        var upload = await (await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/media/upload-url",
            new { contentType = "image/jpeg", sizeBytes = 2048, context = "progress" }, ApiClient.Json)).ReadJsonAsync();
        await MediaTests.UploadToStorageAsync(upload, new byte[2048], "image/jpeg");
        await s.Painter.PostAsJsonAsync($"/api/painter/jobs/{s.Job.JobId}/progress", new { fileIds = new[] { upload.GetProperty("fileId").GetGuid() } }, ApiClient.Json);
        var client = await UserIdAsync(s.Job.Client);

        var notices = await NoticesAsync(s.Job.Code);
        Assert.Contains(notices, n => n.UserId == client && n.Kind == NotificationKind.JobScheduled && n.Name is not null);
        Assert.Contains(notices, n => n.Kind == NotificationKind.OfferAccepted && n.Audience == NoticeAudience.Admin);
        Assert.Contains(notices, n => n.Kind == NotificationKind.JobStarted && n.Audience == NoticeAudience.Admin);
        Assert.Contains(notices, n => n.UserId == client && n.Kind == NotificationKind.ProgressPhotos && n.Audience == NoticeAudience.Client);
        Assert.Contains(notices, n => n.Kind == NotificationKind.ProgressPhotos && n.Audience == NoticeAudience.Admin);
    }

    [Fact]
    public async Task The_demo_data_is_seeded_without_notices()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var demoClient = await scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Pinta.Modules.Identity.Domain.User>>()
            .FindByEmailAsync("ana@demo.pinta.pt");
        var notices = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Notifications;

        Assert.NotNull(demoClient);
        Assert.False(await notices.AnyAsync(n => n.UserId == demoClient.Id));
    }
}
