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

    public required string AddressLine1 { get; set; }

    public string? Floor { get; set; }

    public required string PostalCode { get; init; }

    public required string Locality { get; init; }

    public required string Municipality { get; init; }

    public string? AccessNotes { get; set; }

    public string? Description { get; set; }

    public bool PreferredAsap { get; init; }

    public DateOnly? PreferredFrom { get; init; }

    public DateOnly? PreferredTo { get; init; }

    public bool IsPremium { get; set; }

    public bool RequiresSiteVisit { get; set; }

    public string? TaxId { get; set; }

    public string? CompanyName { get; set; }

    /// <summary>The estimate until the admin confirms a price; then the confirmed version the client decides on.</summary>
    public Guid CurrentQuoteVersionId { get; set; }

    /// <summary>Last day the confirmed price can be accepted (Lisbon); the next day at 03:00 it expires (S3).</summary>
    public DateOnly? QuoteValidUntil { get; set; }

    /// <summary>Site visit (A2.6): when, who goes (a partner or a painter, by user id) and its price with VAT.</summary>
    public DateTimeOffset? SiteVisitAt { get; set; }

    public Guid? SiteVisitAssigneeId { get; set; }

    public decimal? SiteVisitPrice { get; set; }

    /// <summary>The painter's report (P3.1): notes for the reviewer and when it was sent. Measures go to the draft.</summary>
    public string? SiteVisitNotes { get; set; }

    public DateTimeOffset? SiteVisitReportedAt { get; set; }

    /// <summary>The day the work starts (H5: set by the admin; H6: from the painter assignment). Refunds count down to it.</summary>
    public DateOnly? ScheduledOn { get; set; }

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

/// <summary>
/// An offer of a job to a painter (docs/data-model.md, ASIGNACION; A3.2): the date and the painter's pay are frozen when
/// the admin makes it. Accepting it schedules the job.
/// </summary>
public sealed class Assignment
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid JobId { get; init; }

    public Guid PainterId { get; init; }

    public DateOnly ScheduledOn { get; init; }

    /// <summary>What the painter gets for the job (labour and extras), VAT not applicable.</summary>
    public decimal PainterPay { get; init; }

    public AssignmentStatus Status { get; set; }

    public string? DeclineReason { get; set; }

    public DateTimeOffset OfferedAt { get; init; }

    public DateTimeOffset? RespondedAt { get; set; }

    /// <summary>When the admins were told the offer is still unanswered after 48 h (P1.2).</summary>
    public DateTimeOffset? ReminderSentAt { get; set; }

    public uint Version { get; private set; }
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
