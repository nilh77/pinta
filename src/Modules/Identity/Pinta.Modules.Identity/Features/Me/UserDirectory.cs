using Microsoft.AspNetCore.Identity;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Identity.Domain;

namespace Pinta.Modules.Identity.Features.Me;

internal sealed class UserDirectory(UserManager<User> users) : IUserDirectory
{
    public async Task<UserSummary?> FindAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is null ? null : new UserSummary(user.Id, user.Email!, user.Name, user.Locale, user.EmailConfirmed);
    }
}
