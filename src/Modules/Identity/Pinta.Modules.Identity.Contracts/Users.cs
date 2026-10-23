using Pinta.SharedKernel;
using Pinta.SharedKernel.Events;

namespace Pinta.Modules.Identity.Contracts;

/// <summary>What other modules may know about a user.</summary>
public sealed record UserSummary(Guid Id, string Email, string Name, Locale Locale, bool EmailConfirmed, string? Phone);

public interface IUserDirectory
{
    Task<UserSummary?> FindAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSummary>> FindManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>Ids of the users whose name or email contains <paramref name="term"/> (A3.1 search).</summary>
    Task<IReadOnlyList<Guid>> SearchAsync(string term, CancellationToken cancellationToken = default);

    /// <summary>The admins (the partners): site visit assignees and recipients of the admin emails.</summary>
    Task<IReadOnlyList<UserSummary>> ListAdminsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Email confirmation links (valid 48 h) for emails sent by other modules (C4.4, C4.5).</summary>
public interface IEmailConfirmationLinks
{
    Task<string> CreateAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>The user confirmed the email: pending requests can be reviewed (C4.5).</summary>
public sealed record EmailConfirmed(Guid UserId) : IIntegrationEvent;
