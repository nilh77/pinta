using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Data;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Identity.Domain;
using Pinta.SharedKernel;

namespace Pinta.Api.Seed;

/// <summary>Demo data on an empty database in Development (docs/plan.md, "Datos de demo"): the catalog and the two admins.</summary>
public static class DemoSeeder
{
    private static readonly (string Name, string Email)[] Admins =
    [
        ("Nathan", "nathan@demo.pinta.pt"),
        ("Socio pintor", "socio@demo.pinta.pt"),
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        await CatalogSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<CatalogDbContext>(), now);

        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        if (await users.Users.AnyAsync())
        {
            return;
        }

        var password = scope.ServiceProvider.GetRequiredService<IConfiguration>()["Seed:DemoPassword"]
            ?? throw new InvalidOperationException("Seed:DemoPassword is not configured.");

        foreach (var (name, email) in Admins)
        {
            var admin = new User
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Name = name,
                Locale = Locale.Pt,
                CreatedAt = now,
                TermsAcceptedAt = now,
            };
            Ensure(await users.CreateAsync(admin, password));
            Ensure(await users.AddToRoleAsync(admin, RoleNames.Admin));
        }
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
