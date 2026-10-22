using Pinta.Modules.Jobs.Contracts;

namespace Pinta.Modules.Jobs.Domain;

/// <summary>
/// A client's request, from submission to closing (docs/glossary.md). The status only changes through
/// <see cref="TransitionTo"/>, which follows <see cref="JobStateMachine"/> and writes the history.
/// </summary>
public sealed class Job
{
    private readonly List<JobStatusChange> _history = [];

    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Public code <c>PT-YYYY-NNNN</c>.</summary>
    public required string Code { get; init; }

    public Guid ClientId { get; init; }

    public JobStatus Status { get; private set; }

    public required string AddressLine1 { get; init; }

    public string? Floor { get; init; }

    public required string PostalCode { get; init; }

    public required string Locality { get; init; }

    public required string Municipality { get; init; }

    public string? AccessNotes { get; init; }

    public string? Description { get; init; }

    public bool PreferredAsap { get; init; }

    public DateOnly? PreferredFrom { get; init; }

    public DateOnly? PreferredTo { get; init; }

    public bool IsPremium { get; set; }

    public bool RequiresSiteVisit { get; set; }

    public string? TaxId { get; init; }

    public string? CompanyName { get; init; }

    public Guid CurrentQuoteVersionId { get; set; }

    /// <summary>End of the review deadline (2 working days by default) once the job is in review.</summary>
    public DateOnly? ReviewDueOn { get; private set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public uint Version { get; private set; }

    public IReadOnlyList<JobStatusChange> History => _history;

    /// <summary>The first state of a new job: in review when the email is confirmed, otherwise pending email (C4.4).</summary>
    public void Submit(bool emailConfirmed, DateTimeOffset now, Func<DateOnly> reviewDueOn) =>
        Move(null, emailConfirmed ? JobStatus.InReview : JobStatus.PendingEmail, JobActor.Client, ClientId, null, now, reviewDueOn);

    public void TransitionTo(JobStatus to, JobActor actor, Guid? userId, string? reason, DateTimeOffset now, Func<DateOnly>? reviewDueOn = null) =>
        Move(Status, to, actor, userId, reason, now, reviewDueOn);

    private void Move(JobStatus? from, JobStatus to, JobActor actor, Guid? userId, string? reason, DateTimeOffset now, Func<DateOnly>? reviewDueOn)
    {
        if (!JobStateMachine.IsAllowed(from, to, actor))
        {
            throw new InvalidTransitionException(from, to, actor);
        }

        Status = to;
        UpdatedAt = now;
        if (to == JobStatus.InReview && reviewDueOn is not null)
        {
            ReviewDueOn = reviewDueOn();
        }

        _history.Add(new JobStatusChange
        {
            JobId = Id,
            From = from,
            To = to,
            Actor = actor,
            UserId = userId,
            Reason = reason,
            ChangedAt = now,
        });
    }
}

/// <summary>One row of the status history (docs/data-model.md, HISTORIAL_ESTADO).</summary>
public sealed class JobStatusChange
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid JobId { get; init; }

    public JobStatus? From { get; init; }

    public JobStatus To { get; init; }

    public JobActor Actor { get; init; }

    public Guid? UserId { get; init; }

    public string? Reason { get; init; }

    public DateTimeOffset ChangedAt { get; init; }
}
