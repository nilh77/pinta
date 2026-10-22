using Pinta.SharedKernel;

namespace Pinta.Modules.Notifications.Contracts;

/// <summary>An email to send, rendered in <see cref="Locale"/>.</summary>
public abstract record Email(string To, Locale Locale);

/// <summary>Email confirmation link after registration (C4.5).</summary>
public sealed record ConfirmEmailEmail(string To, Locale Locale, string Name, string ConfirmationUrl) : Email(To, Locale);
