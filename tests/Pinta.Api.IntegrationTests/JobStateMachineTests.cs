using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Domain;

namespace Pinta.Api.IntegrationTests;

/// <summary>The table of docs/states.md (CLAUDE.md rule 6): listed transitions only, by the listed actors.</summary>
public class JobStateMachineTests
{
    public static TheoryData<JobStatus?, JobStatus, JobActor> Allowed => new()
    {
        { null, JobStatus.PendingEmail, JobActor.Client },
        { null, JobStatus.InReview, JobActor.Client },
        { JobStatus.PendingEmail, JobStatus.InReview, JobActor.System },
        { JobStatus.InReview, JobStatus.SiteVisit, JobActor.Admin },
        { JobStatus.SiteVisit, JobStatus.InReview, JobActor.Painter },
        { JobStatus.InReview, JobStatus.Quoted, JobActor.Admin },
        { JobStatus.InReview, JobStatus.Rejected, JobActor.Admin },
        { JobStatus.Quoted, JobStatus.DepositPaid, JobActor.System },
        { JobStatus.Quoted, JobStatus.Expired, JobActor.System },
        { JobStatus.Expired, JobStatus.InReview, JobActor.Client },
        { JobStatus.DepositPaid, JobStatus.Scheduled, JobActor.Admin },
        { JobStatus.Scheduled, JobStatus.InProgress, JobActor.Painter },
        { JobStatus.InProgress, JobStatus.Finished, JobActor.Painter },
        { JobStatus.Finished, JobStatus.Closed, JobActor.System },
        { JobStatus.Scheduled, JobStatus.Cancelled, JobActor.Client },
        { JobStatus.PendingEmail, JobStatus.Cancelled, JobActor.Admin },
    };

    public static TheoryData<JobStatus?, JobStatus, JobActor> Forbidden => new()
    {
        { null, JobStatus.Quoted, JobActor.Admin },
        { JobStatus.InReview, JobStatus.Closed, JobActor.System },
        { JobStatus.Quoted, JobStatus.DepositPaid, JobActor.Admin },
        { JobStatus.InReview, JobStatus.Quoted, JobActor.Client },
        { JobStatus.InProgress, JobStatus.Cancelled, JobActor.Client },
        { JobStatus.Finished, JobStatus.Cancelled, JobActor.Admin },
        { JobStatus.Rejected, JobStatus.InReview, JobActor.Client },
        { JobStatus.Closed, JobStatus.InReview, JobActor.Admin },
    };

    [Theory]
    [MemberData(nameof(Allowed))]
    public void Listed_transitions_are_allowed(JobStatus? from, JobStatus to, JobActor actor) =>
        Assert.True(JobStateMachine.IsAllowed(from, to, actor));

    [Theory]
    [MemberData(nameof(Forbidden))]
    public void Other_transitions_are_rejected(JobStatus? from, JobStatus to, JobActor actor) =>
        Assert.False(JobStateMachine.IsAllowed(from, to, actor));

    [Fact]
    public void A_job_writes_every_change_to_its_history_and_refuses_invalid_ones()
    {
        var now = DateTimeOffset.UtcNow;
        var job = new Job { Code = "PT-2026-0001", ClientId = Guid.NewGuid(), AddressLine1 = "Rua", PostalCode = "1000-001", Locality = "Lisboa", Municipality = "Lisboa", CreatedAt = now };

        job.Submit(emailConfirmed: false, now, () => new DateOnly(2026, 10, 27));
        Assert.Equal(JobStatus.PendingEmail, job.Status);
        Assert.Null(job.ReviewDueOn);

        job.TransitionTo(JobStatus.InReview, JobActor.System, null, null, now, () => new DateOnly(2026, 10, 27));
        Assert.Equal(new DateOnly(2026, 10, 27), job.ReviewDueOn);
        Assert.Equal([(JobStatus?)null, JobStatus.PendingEmail], job.History.Select(h => h.From));

        Assert.Throws<InvalidTransitionException>(() => job.TransitionTo(JobStatus.Closed, JobActor.Admin, null, null, now));
        Assert.Equal(JobStatus.InReview, job.Status);
    }
}
