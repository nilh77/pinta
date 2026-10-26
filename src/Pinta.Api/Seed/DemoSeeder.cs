using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Data;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Identity.Domain;
using Pinta.Modules.Painters.Contracts;
using Pinta.Modules.Painters.Data;
using Pinta.Modules.Painters.Domain;
using Pinta.Modules.Painters.Features;
using Pinta.SharedKernel;

namespace Pinta.Api.Seed;

/// <summary>
/// Demo data on an empty database in Development (docs/plan.md, "Datos de demo"): the catalog, the two admins and the
/// three painters (Rui and Carla signed up, João only invited), and the finance month of <see cref="DemoJobsSeeder"/>.
/// </summary>
public static class DemoSeeder
{
    private static readonly (string Name, string Email)[] Admins =
    [
        ("Nathan", "nathan@demo.pinta.pt"),
        ("Socio pintor", "socio@demo.pinta.pt"),
    ];

    private static readonly (string Name, string Email, string Phone, string[] Zones, decimal Rating, string Iban, bool SignedUp)[] Painters =
    [
        ("Rui Almeida", "rui@demo.pinta.pt", "+351912000001", ["Lisboa", "Oeiras", "Amadora"], 4.8m, "PT50000201231234567890154", true),
        ("Carla Santos", "carla@demo.pinta.pt", "+351912000002", ["Sintra", "Cascais"], 4.6m, "PT50003501239876543210921", true),
        ("João Ferreira", "joao@demo.pinta.pt", "+351912000003", ["Loures"], 0m, "", false),
    ];

    /// <summary>The emails of the two partners: a reset keeps their accounts, so whoever presses it stays signed in.</summary>
    public static IReadOnlyList<string> AdminEmails => [.. Admins.Select(a => a.Email)];

    /// <summary>
    /// On an empty database everything; with <paramref name="adminsKept"/> (after a reset) everything but the admins,
    /// who are already there.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services, bool adminsKept = false)
    {
        await using var scope = services.CreateAsyncScope();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        await CatalogSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<CatalogDbContext>(), now);

        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        if (!adminsKept && await users.Users.AnyAsync())
        {
            return;
        }

        var password = scope.ServiceProvider.GetRequiredService<IConfiguration>()["Seed:DemoPassword"]
            ?? throw new InvalidOperationException("Seed:DemoPassword is not configured.");

        foreach (var (name, email) in adminsKept ? [] : Admins)
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

        var painters = scope.ServiceProvider.GetRequiredService<PaintersDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<IbanProtector>();
        foreach (var (name, email, phone, zones, rating, iban, signedUp) in Painters)
        {
            var user = new User { UserName = email, Email = email, EmailConfirmed = signedUp, PhoneNumber = phone, Name = name, Locale = Locale.Pt, CreatedAt = now };
            Ensure(signedUp ? await users.CreateAsync(user, password) : await users.CreateAsync(user));
            Ensure(await users.AddToRoleAsync(user, RoleNames.Painter));
            var painter = new Painter
            {
                UserId = user.Id,
                Name = name,
                Email = email,
                Phone = phone,
                Specialties = ["interior", "exterior"],
                Municipalities = [.. zones],
                Rating = signedUp ? rating : null,
                Status = signedUp ? PainterStatus.Active : PainterStatus.Invited,
                IbanEncrypted = iban.Length > 0 ? protector.Protect(iban) : null,
                IbanLast4 = iban.Length > 0 ? iban[^4..] : null,
            };
            painter.Stamp(now, null);
            painters.Painters.Add(painter);
        }

        await painters.SaveChangesAsync();
        await DemoJobsSeeder.SeedAsync(scope.ServiceProvider, users, password, now);
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
