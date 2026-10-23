using Microsoft.AspNetCore.Identity;
using Pinta.SharedKernel;

namespace Pinta.Modules.Identity.Domain;

/// <summary>Identity user extended with name, language and consent (docs/data-model.md, USUARIO).</summary>
public sealed class User : IdentityUser<Guid>
{
    public User()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public string Name { get; set; } = "";
    public Locale Locale { get; set; } = Locale.Pt;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? TermsAcceptedAt { get; set; }
    public bool MarketingConsent { get; set; }

    /// <summary>Deactivated by the admin (painters, A4.2): cannot sign in by any means until reactivated.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Default invoice details (screen 12), proposed when the client asks for an invoice.</summary>
    public string? TaxId { get; set; }

    public string? CompanyName { get; set; }

    /// <summary>C5.8: the client asked to delete the account; the admin executes it.</summary>
    public DateTimeOffset? DeletionRequestedAt { get; set; }

    /// <summary>When the admin executed the deletion: the personal data is gone and the account cannot sign in.</summary>
    public DateTimeOffset? AnonymizedAt { get; set; }
}
