using Pinta.SharedKernel;
using Pinta.SharedKernel.Events;

namespace Pinta.Modules.Identity.Contracts;

/// <summary>A painter set the password with the invitation: the painter becomes active (A4.2).</summary>
public sealed record PainterSignedUp(Guid UserId) : IIntegrationEvent;

/// <summary>Painter accounts (A4.1, A4.2): created by the admin without password, activated by the invitation.</summary>
public interface IPainterAccounts
{
    /// <summary>A new user with the painter role and no password; null when the email already has an account.</summary>
    Task<Guid?> CreateAsync(string name, string email, string? phone, Locale locale, CancellationToken cancellationToken = default);

    /// <summary>A link to the web onboarding, valid 72 h; a new one invalidates nothing until the password is set.</summary>
    Task<string> InvitationLinkAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Whether the painter has set a password through the invitation.</summary>
    Task<bool> HasSignedUpAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Deactivated users cannot sign in and their sessions end at the next check.</summary>
    Task SetActiveAsync(Guid userId, bool active, CancellationToken cancellationToken = default);

    Task UpdateContactAsync(Guid userId, string name, string? phone, CancellationToken cancellationToken = default);

    /// <summary>Asked again before sensitive changes such as the IBAN (screen P4).</summary>
    Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default);
}
