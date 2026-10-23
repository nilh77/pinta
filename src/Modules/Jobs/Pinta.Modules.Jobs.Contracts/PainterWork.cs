namespace Pinta.Modules.Jobs.Contracts;

/// <summary>Serialized as <c>offered</c>, <c>accepted</c>, <c>declined</c>, <c>withdrawn</c>, <c>completed</c> (docs/api.md).</summary>
public enum AssignmentStatus
{
    Offered,
    Accepted,
    Declined,
    Withdrawn,
    Completed,
}

/// <summary>A job of a painter for the admin's painter page (A4.4): never the client price.</summary>
public sealed record PainterJob(Guid JobId, string Code, JobStatus Status, AssignmentStatus Assignment, DateOnly ScheduledOn, long PainterPayCents);

public interface IPainterWork
{
    Task<IReadOnlyList<PainterJob>> ForPainterAsync(Guid painterId, CancellationToken cancellationToken = default);
}
