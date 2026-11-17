namespace Pinta.Modules.Notifications.Contracts;

/// <summary>What happened (docs/integrations.md, «Avisos y notificaciones push»); serialized in snake_case.</summary>
public enum NotificationKind
{
    RequestReceived,
    ClientQuestion,
    QuoteConfirmed,
    QuoteRejected,
    QuoteExpiring,
    QuoteExpired,
    RequestRejected,
    SiteVisitScheduled,
    SiteVisitAssigned,
    SiteVisitReported,
    MultibancoReference,
    PaymentReceived,
    DepositPaid,
    BalancePaid,
    OfferReceived,
    OfferWithdrawn,
    OfferAccepted,
    OfferDeclined,
    OfferUnanswered,
    JobScheduled,
    JobStarted,
    ProgressPhotos,
    IncidentReported,
    JobFinished,
    BalanceDue,
    JobCancelled,
    RefundIssued,
    RefundCompleted,
    PaymentDispute,
    PayoutPaid,
    AccountDeletionRequested,
    ReviewsOverdue,
    PainterDocumentsExpiring,
}

/// <summary>Whose area the notice opens in: the push links to the matching screen of the job.</summary>
public enum NoticeAudience
{
    Client,
    Painter,
    Admin,
}

/// <summary>
/// One notice. <paramref name="Data"/> fills the text: <c>name</c> (client or painter), <c>amountCents</c>, <c>reason</c>,
/// <c>count</c>, <c>date</c> (ISO). The web writes the text in the interface's language; the push, in the user's.
/// </summary>
public sealed record Notice(NotificationKind Kind, NoticeAudience Audience, Guid? JobId = null, string? JobCode = null, IReadOnlyDictionary<string, string>? Data = null);

/// <summary>Creates notices for users (the bell) and sends them as web push where enabled. Never fails the caller.</summary>
public interface INotifier
{
    Task NotifyAsync(IReadOnlyCollection<Guid> userIds, Notice notice, CancellationToken cancellationToken = default);

    /// <summary>Every admin (the partners) gets <paramref name="notice"/>, whose audience must be <see cref="NoticeAudience.Admin"/>.</summary>
    Task NotifyAdminsAsync(Notice notice, CancellationToken cancellationToken = default);
}
