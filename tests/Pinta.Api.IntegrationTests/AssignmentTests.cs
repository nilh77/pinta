using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Jobs.Features.Assignments;
using Pinta.Modules.Notifications.Contracts;

namespace Pinta.Api.IntegrationTests;

public static class TestAssignments
{
    public sealed record Setup(TestJobs.SentJob Job, HttpClient Admin, HttpClient Painter, Guid PainterId, string PainterEmail);

    /// <summary>TC01 in Sintra with the deposit paid, and a signed-up painter covering Sintra.</summary>
    public static async Task<Setup> ReadyAsync(PintaApiFactory factory)
    {
        var (job, _) = await TestPayments.PaidDepositAsync(factory);
        var admin = await ApiClient.CreateAdminAsync(factory);
        var (painterId, email, link) = await TestPainterAccounts.CreateAsync(factory, admin);
        var painter = await TestPainterAccounts.SignUpAsync(factory, link);
        return new Setup(job, admin, painter, painterId, email);
    }

    public static Task<HttpResponseMessage> OfferAsync(HttpClient admin, Guid jobId, Guid painterId, int inDays = 0) =>
        admin.PostAsJsonAsync($"/api/admin/jobs/{jobId}/assignments",
            new { painterId, scheduledOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(inDays) }, ApiClient.Json);

    public static async Task<Guid> OfferIdAsync(HttpClient painter) =>
        (await (await painter.GetAsync("/api/painter/jobs")).ReadJsonAsync()).GetProperty("offers")[0].GetProperty("assignmentId").GetGuid();

    /// <summary>The painter accepts: the job is scheduled for today unless told otherwise.</summary>
    public static async Task<Setup> ScheduledAsync(PintaApiFactory factory, int inDays = 0)
    {
        var setup = await ReadyAsync(factory);
        (await OfferAsync(setup.Admin, setup.Job.JobId, setup.PainterId, inDays)).EnsureSuccessStatusCode();
        var accept = await setup.Painter.PostAsJsonAsync($"/api/painter/assignments/{await OfferIdAsync(setup.Painter)}/accept", new { }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);
        return setup;
    }
}

/// <summary>A3.2, P1.1, P1.2: offers to painters covering the concelho, accepted or declined.</summary>
public class AssignmentTests(PintaApiFactory factory)
{
    [Fact]
    public async Task A3_2_P1_2_the_painter_accepts_the_offer_and_the_job_is_scheduled_with_its_date()
    {
        var s = await TestAssignments.ReadyAsync(factory);

        var assignable = await (await s.Admin.GetAsync($"/api/admin/jobs/{s.Job.JobId}/assignable-painters")).ReadJsonAsync();
        Assert.Contains(assignable.EnumerateArray(), p => p.GetProperty("id").GetGuid() == s.PainterId);
        var offer = await TestAssignments.OfferAsync(s.Admin, s.Job.JobId, s.PainterId, 5);
        Assert.Equal(HttpStatusCode.OK, offer.StatusCode);
        Assert.Equal(21184, Assert.Single(factory.Emails.To<AssignmentOfferedEmail>(s.PainterEmail)).PainterPayCents);

        var jobs = await (await s.Painter.GetAsync("/api/painter/jobs")).ReadJsonAsync();
        var item = Assert.Single(jobs.GetProperty("offers").EnumerateArray());
        Assert.Equal((s.Job.Code, "Sintra", 21184L, 48.4m), (item.GetProperty("code").GetString(), item.GetProperty("municipality").GetString(),
            item.GetProperty("painterPayCents").GetInt64(), item.GetProperty("areaM2").GetDecimal()));

        var accept = await s.Painter.PostAsJsonAsync($"/api/painter/assignments/{item.GetProperty("assignmentId").GetGuid()}/accept", new { }, ApiClient.Json);
        var again = await s.Painter.PostAsJsonAsync($"/api/painter/assignments/{item.GetProperty("assignmentId").GetGuid()}/accept", new { }, ApiClient.Json);

        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);
        await ProblemAssert.HasCodeAsync(again, HttpStatusCode.Conflict, "assignment_taken");
        var detail = await (await s.Job.Client.GetAsync($"/api/jobs/{s.Job.JobId}")).ReadJsonAsync();
        Assert.Equal(("scheduled", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5).ToString("yyyy-MM-dd")),
            (detail.GetProperty("status").GetString(), detail.GetProperty("scheduledOn").GetString()));
        Assert.Equal("Rui", Assert.Single(factory.Emails.To<JobScheduledEmail>(s.Job.Email)).PainterName);
        Assert.Single((await (await s.Painter.GetAsync("/api/painter/jobs")).ReadJsonAsync()).GetProperty("upcoming").EnumerateArray());
        var admin = await (await s.Admin.GetAsync($"/api/admin/jobs/{s.Job.JobId}")).ReadJsonAsync();
        Assert.Equal("accepted", Assert.Single(admin.GetProperty("assignments").EnumerateArray()).GetProperty("status").GetString());
        var painter = await (await s.Admin.GetAsync($"/api/admin/painters/{s.PainterId}")).ReadJsonAsync();
        Assert.Equal(s.Job.Code, Assert.Single(painter.GetProperty("jobs").EnumerateArray()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A3_2_only_active_painters_covering_the_concelho_and_only_paid_jobs()
    {
        var s = await TestAssignments.ReadyAsync(factory);
        var (lisbon, _, lisbonLink) = await TestPainterAccounts.CreateAsync(factory, s.Admin, ["Lisboa"]);
        await TestPainterAccounts.SignUpAsync(factory, lisbonLink);
        var unpaid = await TestJobs.SendAsync(factory);

        await ProblemAssert.HasCodeAsync(await TestAssignments.OfferAsync(s.Admin, s.Job.JobId, lisbon), HttpStatusCode.UnprocessableEntity, "validation_failed");
        await ProblemAssert.HasCodeAsync(await TestAssignments.OfferAsync(s.Admin, s.Job.JobId, s.PainterId, -1), HttpStatusCode.UnprocessableEntity, "validation_failed");
        await ProblemAssert.HasCodeAsync(await TestAssignments.OfferAsync(s.Admin, unpaid.JobId, s.PainterId), HttpStatusCode.Conflict, "invalid_transition");
        await s.Admin.PostAsJsonAsync($"/api/admin/painters/{s.PainterId}/deactivate", new { }, ApiClient.Json);
        await ProblemAssert.HasCodeAsync(await TestAssignments.OfferAsync(s.Admin, s.Job.JobId, s.PainterId), HttpStatusCode.UnprocessableEntity, "validation_failed");
    }

    [Fact]
    public async Task P1_2_a_new_offer_withdraws_the_previous_one_and_declining_notifies_the_admins()
    {
        var s = await TestAssignments.ReadyAsync(factory);
        var (otherId, _, otherLink) = await TestPainterAccounts.CreateAsync(factory, s.Admin);
        var other = await TestPainterAccounts.SignUpAsync(factory, otherLink);

        await TestAssignments.OfferAsync(s.Admin, s.Job.JobId, s.PainterId);
        var first = await TestAssignments.OfferIdAsync(s.Painter);
        await TestAssignments.OfferAsync(s.Admin, s.Job.JobId, otherId);
        var late = await s.Painter.PostAsJsonAsync($"/api/painter/assignments/{first}/accept", new { }, ApiClient.Json);
        var foreign = await s.Painter.PostAsJsonAsync($"/api/painter/assignments/{await TestAssignments.OfferIdAsync(other)}/accept", new { }, ApiClient.Json);
        var noReason = await other.PostAsJsonAsync($"/api/painter/assignments/{await TestAssignments.OfferIdAsync(other)}/decline", new { reason = "" }, ApiClient.Json);
        var declined = await other.PostAsJsonAsync($"/api/painter/assignments/{await TestAssignments.OfferIdAsync(other)}/decline", new { reason = "Estou de férias." }, ApiClient.Json);

        await ProblemAssert.HasCodeAsync(late, HttpStatusCode.Conflict, "assignment_taken");
        await ProblemAssert.HasCodeAsync(foreign, HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.HasCodeAsync(noReason, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal(HttpStatusCode.NoContent, declined.StatusCode);
        Assert.Contains(factory.Emails.To<AssignmentDeclinedEmail>(TestData.AdminEmail), e => e.Code == s.Job.Code && e.Reason == "Estou de férias.");
        Assert.Equal("deposit_paid", (await (await s.Job.Client.GetAsync($"/api/jobs/{s.Job.JobId}")).ReadJsonAsync()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task P1_2_an_offer_unanswered_for_48_hours_is_reported_once()
    {
        var s = await TestAssignments.ReadyAsync(factory);
        await TestAssignments.OfferAsync(s.Admin, s.Job.JobId, s.PainterId);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE jobs.assignments SET offered_at = now() - interval '49 hours' WHERE job_id = {s.Job.JobId}");
        var reminders = scope.ServiceProvider.GetRequiredService<OfferReminders>();

        await reminders.RunAsync();
        await reminders.RunAsync();

        Assert.Single(factory.Emails.To<AssignmentUnansweredEmail>(TestData.AdminEmail), e => e.Code == s.Job.Code);
    }
}
