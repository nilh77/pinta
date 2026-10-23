using Pinta.SharedKernel;

namespace Pinta.Modules.Notifications.Contracts;

/// <summary>
/// An email to send, rendered in <see cref="Locale"/>. <see cref="JobId"/> ties it to a job so it appears in the job's
/// history (A3.5).
/// </summary>
public abstract record Email(string To, Locale Locale)
{
    public Guid? JobId { get; init; }
}

/// <summary>Email confirmation link after registration (C4.5).</summary>
public sealed record ConfirmEmailEmail(string To, Locale Locale, string Name, string ConfirmationUrl) : Email(To, Locale);

/// <summary>
/// The request was received (C4.4): code, area and range, and the confirmation link when the email is not confirmed yet
/// (the request waits in "pending email" until then).
/// </summary>
public sealed record RequestReceivedEmail(
    string To,
    Locale Locale,
    string Name,
    string Code,
    decimal AreaM2,
    long? RangeMinCents,
    long? RangeMaxCents,
    int ReviewDays,
    string? ConfirmationUrl) : Email(To, Locale);

/// <summary>The admin confirmed the price (A2.8): total, deposit, validity and the reviewer's note.</summary>
public sealed record PriceConfirmedEmail(
    string To, Locale Locale, string Name, string Code, long TotalCents, long DepositCents, DateOnly ValidUntil, string? Note, string JobUrl)
    : Email(To, Locale);

/// <summary>The admin rejected the request with a reason (A2.9).</summary>
public sealed record RequestRejectedEmail(string To, Locale Locale, string Name, string Code, string Reason) : Email(To, Locale);

/// <summary>The confirmed price was not paid in time (S3).</summary>
public sealed record PriceExpiredEmail(string To, Locale Locale, string Name, string Code, string JobUrl) : Email(To, Locale);

/// <summary>A site visit was scheduled (A2.6).</summary>
public sealed record SiteVisitScheduledEmail(
    string To, Locale Locale, string Name, string Code, DateTimeOffset VisitAt, string VisitorName, long PriceCents) : Email(To, Locale);

/// <summary>To the admins: a client asks about the confirmed price ("Tengo dudas", C5.2).</summary>
public sealed record ClientQuestionEmail(
    string To, Locale Locale, string Code, string ClientName, string ClientEmail, string Message, string AdminUrl) : Email(To, Locale);

/// <summary>To the admins: a client rejected the confirmed price (C5.2).</summary>
public sealed record ClientRejectedPriceEmail(
    string To, Locale Locale, string Code, string ClientName, string? Reason, string AdminUrl) : Email(To, Locale);

/// <summary>Password recovery link, valid 2 hours (S1).</summary>
public sealed record ResetPasswordEmail(string To, Locale Locale, string Name, string ResetUrl) : Email(To, Locale);

/// <summary>
/// A payment arrived (webhook): <see cref="Kind"/> is <c>deposit</c>, <c>balance</c> or <c>site_visit</c>. MB WAY payments
/// show "Stripe" on the statement (docs/integrations.md).
/// </summary>
public sealed record PaymentReceivedEmail(
    string To, Locale Locale, string Name, string Code, string Kind, long AmountCents, bool MbWay, string JobUrl) : Email(To, Locale);

/// <summary>The Multibanco reference to pay at a bank or ATM (C5.3).</summary>
public sealed record MultibancoReferenceEmail(
    string To, Locale Locale, string Name, string Code, string Entity, string Reference, long AmountCents, DateTimeOffset? ExpiresAt, string JobUrl)
    : Email(To, Locale);

/// <summary>A refund was requested to Stripe after a cancellation (C5.6).</summary>
public sealed record RefundEmail(string To, Locale Locale, string Name, string Code, long AmountCents, string JobUrl) : Email(To, Locale);

/// <summary>To the admins: a payment arrived for a job that could no longer take it and was refunded in full (S4).</summary>
public sealed record AutoRefundEmail(string To, Locale Locale, string Code, string Kind, long AmountCents, string AdminUrl) : Email(To, Locale);

/// <summary>To the admins: a client opened a dispute in Stripe (S4).</summary>
public sealed record PaymentDisputeEmail(string To, Locale Locale, string Code, long AmountCents, string? AdminUrl) : Email(To, Locale);

/// <summary>The admin invited a painter: link to sign up, valid 72 h (A4.2).</summary>
public sealed record PainterInvitationEmail(string To, Locale Locale, string Name, string SignUpUrl) : Email(To, Locale);

/// <summary>To a painter: a job is offered with its date and pay (A3.2).</summary>
public sealed record AssignmentOfferedEmail(
    string To, Locale Locale, string Name, string Code, string Municipality, DateOnly ScheduledOn, long PainterPayCents, string Url) : Email(To, Locale);

/// <summary>To the admins: a painter declined an offer (P1.2).</summary>
public sealed record AssignmentDeclinedEmail(string To, Locale Locale, string Code, string PainterName, string Reason, string AdminUrl) : Email(To, Locale);

/// <summary>To the admins: an offer is still unanswered after 48 h (P1.2).</summary>
public sealed record AssignmentUnansweredEmail(string To, Locale Locale, string Code, string PainterName, string AdminUrl) : Email(To, Locale);

/// <summary>To the client: the job has a painter and a start date (A3.2).</summary>
public sealed record JobScheduledEmail(string To, Locale Locale, string Name, string Code, DateOnly ScheduledOn, string PainterName, string JobUrl)
    : Email(To, Locale);

/// <summary>To the client: the painter finished; the balance is due (P2.2, C5.5).</summary>
public sealed record JobFinishedEmail(string To, Locale Locale, string Name, string Code, long BalanceCents, string JobUrl) : Email(To, Locale);

/// <summary>To a painter: a site visit is assigned, without the price the client pays (A2.6).</summary>
public sealed record SiteVisitAssignedEmail(string To, Locale Locale, string Name, string Code, DateTimeOffset VisitAt, string Municipality, string Url)
    : Email(To, Locale);

/// <summary>To the admins: the painter sent the site visit report; the draft has the real measures (P3.1).</summary>
public sealed record SiteVisitReportedEmail(string To, Locale Locale, string Code, string PainterName, string AdminUrl) : Email(To, Locale);

/// <summary>To the admins: a client asked to delete their account; they execute it from Ajustes (C5.8).</summary>
public sealed record AccountDeletionRequestedEmail(string To, Locale Locale, string ClientName, string ClientEmail, string AdminUrl) : Email(To, Locale);

/// <summary>To the client, 3 days before the confirmed price expires (S3).</summary>
public sealed record PriceExpiringEmail(string To, Locale Locale, string Name, string Code, DateOnly ValidUntil, long DepositCents, string JobUrl)
    : Email(To, Locale);

/// <summary>To the admins, daily: requests past their review deadline (S3).</summary>
public sealed record OverdueReviewsEmail(string To, Locale Locale, IReadOnlyList<string> Codes, string AdminUrl) : Email(To, Locale);

/// <summary>To the admins, on Mondays: painter documents that expire within 30 days, as "name · kind · date" lines (S3).</summary>
public sealed record PainterDocumentsExpiringEmail(string To, Locale Locale, IReadOnlyList<string> Lines, string AdminUrl) : Email(To, Locale);
