using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Identity.Domain;

namespace Pinta.Modules.Identity.Features.Me;

internal sealed class UserDirectory(UserManager<User> users) : IUserDirectory
{
    public async Task<UserSummary?> FindAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is null ? null : ToSummary(user);
    }

    public async Task<IReadOnlyList<UserSummary>> FindManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
        userIds.Count == 0
            ? []
            : (await users.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToListAsync(cancellationToken)).Select(ToSummary).ToList();

    public async Task<IReadOnlyList<Guid>> SearchAsync(string term, CancellationToken cancellationToken = default)
    {
        var pattern = $"%{term.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
        return await users.Users.AsNoTracking()
            .Where(u => EF.Functions.ILike(u.Name, pattern) || EF.Functions.ILike(u.Email!, pattern))
            .Select(u => u.Id)
            .Take(500)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserSummary>> ListAdminsAsync(CancellationToken cancellationToken = default) =>
        (await users.GetUsersInRoleAsync(RoleNames.Admin)).OrderBy(u => u.Name).Select(ToSummary).ToList();

    private static UserSummary ToSummary(User user) =>
        new(user.Id, user.Email!, user.Name, user.Locale, user.EmailConfirmed, user.PhoneNumber);
}
