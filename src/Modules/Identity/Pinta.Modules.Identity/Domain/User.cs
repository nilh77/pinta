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
}
