using Pinta.SharedKernel;

namespace Pinta.Modules.Notifications.Contracts;

/// <summary>An email to send, rendered in <see cref="Locale"/>.</summary>
public abstract record Email(string To, Locale Locale);

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
