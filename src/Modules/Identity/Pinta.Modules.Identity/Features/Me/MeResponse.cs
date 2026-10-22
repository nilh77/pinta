using Microsoft.AspNetCore.Identity;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Identity.Domain;
using Pinta.SharedKernel;

namespace Pinta.Modules.Identity.Features.Me;

public sealed record MeResponse(Guid Id, string Email, string Name, Role Role, Locale Locale, bool EmailConfirmed)
{
    public static async Task<MeResponse> ForAsync(User user, UserManager<User> users)
    {
        var roles = await users.GetRolesAsync(user);
        var role = roles.Count > 0 ? AppRoles.Parse(roles[0]) : Role.Client;
        return new MeResponse(user.Id, user.Email!, user.Name, role, user.Locale, user.EmailConfirmed);
    }
}
