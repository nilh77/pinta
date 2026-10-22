using Pinta.SharedKernel;
using Pinta.SharedKernel.Events;

namespace Pinta.Modules.Identity.Contracts;

/// <summary>What other modules may know about a user.</summary>
public sealed record UserSummary(Guid Id, string Email, string Name, Locale Locale, bool EmailConfirmed);

public interface IUserDirectory
{
    Task<UserSummary?> FindAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>Email confirmation links (valid 48 h) for emails sent by other modules (C4.4, C4.5).</summary>
public interface IEmailConfirmationLinks
{
    Task<string> CreateAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>The user confirmed the email: pending requests can be reviewed (C4.5).</summary>
public sealed record EmailConfirmed(Guid UserId) : IIntegrationEvent;
