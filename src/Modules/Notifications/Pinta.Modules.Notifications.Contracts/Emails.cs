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
