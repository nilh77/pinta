using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Notifications.Data;
using Pinta.Modules.Notifications.Features.Push;

namespace Pinta.Api.IntegrationTests;

/// <summary>The bell and web push (docs/integrations.md, «Avisos y notificaciones push»).</summary>
public class NotificationTests(PintaApiFactory factory)
{
    private async Task<(HttpClient Client, Guid UserId)> UserAsync(string locale = "pt")
    {
        var client = await ApiClient.CreateAsync(factory);
        (await client.RegisterAsync(TestData.UniqueEmail(), locale: locale, sendConfirmationEmail: false)).EnsureSuccessStatusCode();
        var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
        return (client, me.GetProperty("id").GetGuid());
    }

    private async Task NotifyAsync(Guid userId, Notice notice)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<INotifier>().NotifyAsync([userId], notice);
    }

    private static async Task<int> UnreadAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/notifications/unread-count")).ReadJsonAsync()).GetProperty("count").GetInt32();

    [Fact]
    public async Task Notices_are_listed_newest_first_counted_and_marked_read_by_their_owner_only()
    {
        var (ana, anaId) = await UserAsync();
        var (other, _) = await UserAsync();
        var jobId = Guid.NewGuid();
        await NotifyAsync(anaId, new Notice(NotificationKind.QuoteConfirmed, NoticeAudience.Client, jobId, "PT-2026-0901"));
        await NotifyAsync(anaId, new Notice(NotificationKind.JobScheduled, NoticeAudience.Client, jobId, "PT-2026-0901", new Dictionary<string, string> { ["name"] = "Rui" }));

        var list = await (await ana.GetAsync("/api/notifications")).ReadJsonAsync();
        var items = list.GetProperty("items").EnumerateArray().ToList();
        var newest = items[0];
        var foreign = await other.PostAsJsonAsync($"/api/notifications/{newest.GetProperty("id").GetGuid()}/read", new { }, ApiClient.Json);
        var unreadBefore = await UnreadAsync(ana);
        var read = await ana.PostAsJsonAsync($"/api/notifications/{newest.GetProperty("id").GetGuid()}/read", new { }, ApiClient.Json);
        var unreadAfterOne = await UnreadAsync(ana);
        await ana.PostAsJsonAsync("/api/notifications/read-all", new { }, ApiClient.Json);

        Assert.Equal(2, list.GetProperty("total").GetInt32());
        Assert.Equal(("job_scheduled", "PT-2026-0901", "Rui"),
            (newest.GetProperty("kind").GetString(), newest.GetProperty("jobCode").GetString(), newest.GetProperty("data").GetProperty("name").GetString()));
        await ProblemAssert.HasCodeAsync(foreign, HttpStatusCode.NotFound, "not_found");
        Assert.Equal(0, (await (await other.GetAsync("/api/notifications")).ReadJsonAsync()).GetProperty("total").GetInt32());
        Assert.Equal((2, HttpStatusCode.NoContent, 1, 0), (unreadBefore, read.StatusCode, unreadAfterOne, await UnreadAsync(ana)));
    }

    [Fact]
    public async Task Notices_need_a_session()
    {
        var anonymous = await ApiClient.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/notifications")).StatusCode);
    }

    [Fact]
    public async Task Push_goes_to_every_device_in_the_user_language_and_forgets_gone_devices()
    {
        var (ana, anaId) = await UserAsync(locale: "es");
        var key = await (await ana.GetAsync("/api/push/public-key")).ReadJsonAsync();
        var phone = $"https://push.example/{Guid.NewGuid()}";
        var laptop = $"https://push.example/{Guid.NewGuid()}";
        var invalid = await ana.PostAsJsonAsync("/api/push/subscriptions", new { endpoint = "http://insecure.example", p256dh = "k", auth = "a" }, ApiClient.Json);
        foreach (var endpoint in new[] { phone, laptop, phone })
        {
            (await ana.PostAsJsonAsync("/api/push/subscriptions", new { endpoint, p256dh = "BKey", auth = "secret" }, ApiClient.Json)).EnsureSuccessStatusCode();
        }

        factory.Push.Gone[laptop] = true;
        var jobId = Guid.NewGuid();
        await NotifyAsync(anaId, new Notice(NotificationKind.JobScheduled, NoticeAudience.Client, jobId, "PT-2026-0902", new Dictionary<string, string> { ["name"] = "Rui" }));
        Guid noticeId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            noticeId = (await db.Notifications.SingleAsync(n => n.UserId == anaId)).Id;
            await scope.ServiceProvider.GetRequiredService<PushSender>().SendAsync(noticeId);
        }

        await using var check = factory.Services.CreateAsyncScope();
        var devices = await check.ServiceProvider.GetRequiredService<NotificationsDbContext>().PushSubscriptions.Where(s => s.UserId == anaId).ToListAsync();
        var payload = JsonDocument.Parse(factory.Push.To(phone).Last()).RootElement;

        Assert.Equal("test-public-key", key.GetProperty("publicKey").GetString());
        await ProblemAssert.HasCodeAsync(invalid, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal(phone, Assert.Single(devices).Endpoint);
        Assert.Equal(("Pinta · PT-2026-0902", "PT-2026-0902 programado con Rui", $"/es/mis-trabajos/{jobId}"),
            (payload.GetProperty("title").GetString(), payload.GetProperty("body").GetString(), payload.GetProperty("url").GetString()));
    }

    [Fact]
    public async Task Turning_push_off_removes_only_this_device()
    {
        var (ana, anaId) = await UserAsync();
        var endpoint = $"https://push.example/{Guid.NewGuid()}";
        await ana.PostAsJsonAsync("/api/push/subscriptions", new { endpoint, p256dh = "BKey", auth = "secret" }, ApiClient.Json);

        var removed = await ana.DeleteAsync($"/api/push/subscriptions?endpoint={Uri.EscapeDataString(endpoint)}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().PushSubscriptions.AnyAsync(s => s.UserId == anaId));
    }
}
