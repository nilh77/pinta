using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Identity.Domain;
using Pinta.SharedKernel;

namespace Pinta.Modules.Identity.Data;

public sealed class IdentityModuleDbContext(DbContextOptions<IdentityModuleDbContext> options)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public const string Schema = "identity";

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<User>(user =>
        {
            user.ToTable("users");
            user.Property(u => u.Name).HasMaxLength(200);
            user.Property(u => u.Locale)
                .HasConversion(l => l.ToString().ToLowerInvariant(), s => Enum.Parse<Locale>(s, true))
                .HasMaxLength(2);
        });
        builder.Entity<IdentityRole<Guid>>().ToTable("roles").HasData(AppRoles.All);
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<DataProtectionKey>().ToTable("data_protection_keys");
    }
}
