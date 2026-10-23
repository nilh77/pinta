using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Features.Expiry;
using Pinta.Modules.Jobs.Features.Shared;
using Pinta.Modules.Notifications.Contracts;
using Pinta.SharedKernel.Time;

namespace Pinta.Api.IntegrationTests;

public static class TestReview
{
    /// <summary>The admin confirms the price as it is.</summary>
    public static async Task ConfirmAsync(PintaApiFactory factory, Guid jobId)
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var response = await admin.PostAsJsonAsync($"/api/admin/jobs/{jobId}/confirm-quote", new { acknowledgeLowMargin = true }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}

/// <summary>The client's jobs and their actions (C5.1, C5.2, C5.6).</summary>
public class ClientJobsTests(PintaApiFactory factory)
{
    [Fact]
    public async Task C5_1_active_and_history_lists_with_the_job_waiting_for_an_answer_highlighted()
    {
        var job = await TestJobs.SendAsync(factory);
        await TestReview.ConfirmAsync(factory, job.JobId);
        var second = await job.Client.PostAsJsonAsync("/api/jobs", new { }, ApiClient.Json);

        var active = await (await job.Client.GetAsync("/api/jobs?filter=active")).ReadJsonAsync();
        var history = await (await job.Client.GetAsync("/api/jobs?filter=history")).ReadJsonAsync();

        var item = Assert.Single(active.GetProperty("items").EnumerateArray());
        Assert.Equal(("quoted", true, 49225), (item.GetProperty("status").GetString(), item.GetProperty("needsAction").GetBoolean(), item.GetProperty("totalCents").GetInt64()));
        Assert.Equal(0, history.GetProperty("total").GetInt32());
        Assert.NotEqual(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task C5_2_questions_reach_the_admins_without_changing_the_state()
    {
        var job = await TestJobs.SendAsync(factory);
        var tooEarly = await job.Client.PostAsJsonAsync($"/api/jobs/{job.JobId}/questions", new { message = "O teto está incluído?" }, ApiClient.Json);
        await TestReview.ConfirmAsync(factory, job.JobId);

        var asked = await job.Client.PostAsJsonAsync($"/api/jobs/{job.JobId}/questions", new { message = "O teto está incluído?" }, ApiClient.Json);

        await ProblemAssert.HasCodeAsync(tooEarly, HttpStatusCode.Conflict, "invalid_transition");
        Assert.Equal(HttpStatusCode.NoContent, asked.StatusCode);
        Assert.Contains(factory.Emails.To<ClientQuestionEmail>(TestData.AdminEmail), e => e.Code == job.Code && e.ClientEmail == job.Email);
        Assert.Equal("quoted", (await (await job.Client.GetAsync($"/api/jobs/{job.JobId}")).ReadJsonAsync()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task C5_2_rejecting_the_price_cancels_and_notifies_the_admins()
    {
        var job = await TestJobs.SendAsync(factory);
        await TestReview.ConfirmAsync(factory, job.JobId);

        var rejected = await job.Client.PostAsJsonAsync($"/api/jobs/{job.JobId}/reject-quote", new { reason = "Muito caro." }, ApiClient.Json);
        var again = await job.Client.PostAsJsonAsync($"/api/jobs/{job.JobId}/reject-quote", new { }, ApiClient.Json);

        Assert.Equal(HttpStatusCode.NoContent, rejected.StatusCode);
        await ProblemAssert.HasCodeAsync(again, HttpStatusCode.Conflict, "invalid_transition");
        Assert.Contains(factory.Emails.To<ClientRejectedPriceEmail>(TestData.AdminEmail), e => e.Code == job.Code && e.Reason == "Muito caro.");
        var detail = await (await job.Client.GetAsync($"/api/jobs/{job.JobId}")).ReadJsonAsync();
        Assert.Equal(("cancelled", false), (detail.GetProperty("status").GetString(), detail.GetProperty("canCancel").GetBoolean()));
        var history = await (await job.Client.GetAsync("/api/jobs?filter=history")).ReadJsonAsync();
        Assert.Equal(job.JobId, Assert.Single(history.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task C5_6_cancel_shows_the_refund_first_and_works_once()
    {
        var job = await TestJobs.SendAsync(factory);

        var preview = await (await job.Client.GetAsync($"/api/jobs/{job.JobId}/cancellation-preview")).ReadJsonAsync();
        var cancelled = await job.Client.PostAsJsonAsync($"/api/jobs/{job.JobId}/cancel", new { reason = "Mudei de ideias." }, ApiClient.Json);
        var again = await job.Client.PostAsJsonAsync($"/api/jobs/{job.JobId}/cancel", new { }, ApiClient.Json);
        var after = await (await job.Client.GetAsync($"/api/jobs/{job.JobId}/cancellation-preview")).ReadJsonAsync();

        Assert.Equal((true, 0L, 0L), (preview.GetProperty("cancellable").GetBoolean(), preview.GetProperty("depositPaidCents").GetInt64(), preview.GetProperty("refundCents").GetInt64()));
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        await ProblemAssert.HasCodeAsync(again, HttpStatusCode.Conflict, "invalid_transition");
        Assert.False(after.GetProperty("cancellable").GetBoolean());
    }

    [Fact]
    public async Task C5_7_requote_is_only_for_expired_prices()
    {
        var job = await TestJobs.SendAsync(factory);

        await ProblemAssert.HasCodeAsync(await job.Client.PostAsJsonAsync($"/api/jobs/{job.JobId}/requote", new { }, ApiClient.Json),
            HttpStatusCode.Conflict, "invalid_transition");
    }
}

/// <summary>A clock fixed at a given instant, for the daily jobs.</summary>
public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

[CollectionDefinition(DisableParallelization = true)]
public sealed class SerialCollection;

/// <summary>
/// S3: a price confirmed on day D is valid through D+15 and expires on D+16 (03:00 Lisbon). The job runs on the shared
/// database at today's date, so the test moves only its own job's last valid day: other tests' prices are not touched.
/// </summary>
[Collection(nameof(SerialCollection))]
public class QuoteExpiryTests(PintaApiFactory factory)
{
    private async Task<int> RunExpiryAsync(DateTimeOffset now)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        return await new ExpireQuotes(
            services.GetRequiredService<JobsDbContext>(), services.GetRequiredService<IUserDirectory>(), services.GetRequiredService<IEmailQueue>(),
            services.GetRequiredService<Pinta.SharedKernel.Http.WebLinks>(), new FixedClock(now)).RunAsync();
    }

    private static DateTimeOffset At0300Lisbon(DateOnly day)
    {
        var local = day.ToDateTime(new TimeOnly(3, 0));
        return new DateTimeOffset(local, BusinessDays.Lisbon.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>As if the price had been confirmed <paramref name="daysAgo"/> days before today.</summary>
    private async Task ConfirmedDaysAgoAsync(Guid jobId, DateOnly validUntil, int daysAgo)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var shifted = validUntil.AddDays(-daysAgo);
        await scope.ServiceProvider.GetRequiredService<JobsDbContext>().Database
            .ExecuteSqlInterpolatedAsync($"UPDATE jobs.jobs SET quote_valid_until = {shifted} WHERE id = {jobId}");
    }

    [Fact]
    public async Task S3_C5_7_an_unpaid_price_expires_on_day_16_and_the_client_can_ask_again()
    {
        var job = await TestJobs.SendAsync(factory);
        await TestReview.ConfirmAsync(factory, job.JobId);
        var confirmedOn = BusinessDays.Today(TimeProvider.System);

        var confirmed = await (await job.Client.GetAsync($"/api/jobs/{job.JobId}")).ReadJsonAsync();
        var validUntil = DateOnly.Parse(confirmed.GetProperty("quoteValidUntil").GetString()!);
        Assert.Equal(confirmedOn.AddDays(15), validUntil);

        await ConfirmedDaysAgoAsync(job.JobId, validUntil, 15);
        await RunExpiryAsync(At0300Lisbon(confirmedOn));
        var day15 = await (await job.Client.GetAsync($"/api/jobs/{job.JobId}")).ReadJsonAsync();
        await ConfirmedDaysAgoAsync(job.JobId, validUntil, 16);
        await RunExpiryAsync(At0300Lisbon(confirmedOn));
        var day16 = await (await job.Client.GetAsync($"/api/jobs/{job.JobId}")).ReadJsonAsync();

        Assert.Equal("quoted", day15.GetProperty("status").GetString());
        Assert.Equal(("expired", true), (day16.GetProperty("status").GetString(), day16.GetProperty("canRequote").GetBoolean()));
        Assert.Equal(job.JobId, Assert.Single(factory.Emails.To<PriceExpiredEmail>(job.Email)).JobId);

        var requote = await job.Client.PostAsJsonAsync($"/api/jobs/{job.JobId}/requote", new { }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NoContent, requote.StatusCode);
        var admin = await ApiClient.CreateAdminAsync(factory);
        var detail = await (await admin.GetAsync($"/api/admin/jobs/{job.JobId}")).ReadJsonAsync();
        Assert.Equal("in_review", detail.GetProperty("status").GetString());
        Assert.Equal(("revision", 3), (detail.GetProperty("draft").GetProperty("origin").GetString(), detail.GetProperty("draft").GetProperty("number").GetInt32()));
    }
}
