using Microsoft.AspNetCore.Identity;
using Pinta.Modules.Identity.Contracts;

namespace Pinta.Modules.Identity.Domain;

/// <summary>The three roles, seeded by migration with fixed IDs so every environment has them.</summary>
public static class AppRoles
{
    public static readonly IdentityRole<Guid>[] All =
    [
        Create("0199f2a0-0000-7000-8000-000000000001", RoleNames.Client),
        Create("0199f2a0-0000-7000-8000-000000000002", RoleNames.Painter),
        Create("0199f2a0-0000-7000-8000-000000000003", RoleNames.Admin),
    ];

    public static Role Parse(string name) => Enum.Parse<Role>(name, ignoreCase: true);

    private static IdentityRole<Guid> Create(string id, string name) => new()
    {
        Id = Guid.Parse(id),
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = name,
    };
}
