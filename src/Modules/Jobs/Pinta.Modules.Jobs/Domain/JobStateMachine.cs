using Pinta.Modules.Jobs.Contracts;

namespace Pinta.Modules.Jobs.Domain;

/// <summary>Who triggers a transition (docs/states.md, column "Quién").</summary>
public enum JobActor
{
    Client,
    Admin,
    Painter,
    System,
}

/// <summary>Thrown for any transition not in <see cref="JobStateMachine"/>; the API answers 409 invalid_transition.</summary>
public sealed class InvalidTransitionException(JobStatus? from, JobStatus to, JobActor actor)
    : InvalidOperationException($"{actor} cannot move a job from {from?.ToString() ?? "(new)"} to {to}.");

/// <summary>
/// The table of docs/states.md, the only source of the job state machine (CLAUDE.md rule 6). <c>null</c> is a new job.
/// Any pair not listed here is rejected.
/// </summary>
public static class JobStateMachine
{
    /// <summary>States from which the client or the admin may still cancel ("cualquiera antes de En curso").</summary>
    public static readonly JobStatus[] CancellableFrom =
    [
        JobStatus.PendingEmail, JobStatus.InReview, JobStatus.SiteVisit, JobStatus.Quoted, JobStatus.Expired,
        JobStatus.DepositPaid, JobStatus.Scheduled,
    ];

    public static readonly IReadOnlyList<(JobStatus? From, JobStatus To, JobActor[] Actors)> Transitions =
    [
        (null, JobStatus.PendingEmail, [JobActor.Client]),
        (null, JobStatus.InReview, [JobActor.Client, JobActor.System]),
        (JobStatus.PendingEmail, JobStatus.InReview, [JobActor.Client, JobActor.System]),
        (JobStatus.InReview, JobStatus.SiteVisit, [JobActor.Admin]),
        // "Pintor / socio": a partner is an admin.
        (JobStatus.SiteVisit, JobStatus.InReview, [JobActor.Painter, JobActor.Admin]),
        (JobStatus.InReview, JobStatus.Quoted, [JobActor.Admin]),
        (JobStatus.InReview, JobStatus.Rejected, [JobActor.Admin]),
        (JobStatus.Quoted, JobStatus.DepositPaid, [JobActor.System]),
        (JobStatus.Quoted, JobStatus.Expired, [JobActor.System]),
        (JobStatus.Expired, JobStatus.InReview, [JobActor.Client]),
        (JobStatus.DepositPaid, JobStatus.Scheduled, [JobActor.Admin]),
        (JobStatus.Scheduled, JobStatus.InProgress, [JobActor.Painter]),
        (JobStatus.InProgress, JobStatus.Finished, [JobActor.Painter]),
        (JobStatus.Finished, JobStatus.Closed, [JobActor.System]),
        .. CancellableFrom.Select(from => ((JobStatus?)from, JobStatus.Cancelled, new[] { JobActor.Client, JobActor.Admin })),
    ];

    public static bool IsAllowed(JobStatus? from, JobStatus to, JobActor actor) =>
        Transitions.Any(t => t.From == from && t.To == to && t.Actors.Contains(actor));
}
