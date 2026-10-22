namespace Pinta.Modules.Jobs.Contracts;

/// <summary>Job state (docs/glossary.md); serialized as <c>pending_email</c>, <c>in_review</c>, … (docs/api.md).</summary>
public enum JobStatus
{
    PendingEmail,
    InReview,
    SiteVisit,
    Quoted,
    Expired,
    Rejected,
    DepositPaid,
    Scheduled,
    InProgress,
    Finished,
    Closed,
    Cancelled,
}
