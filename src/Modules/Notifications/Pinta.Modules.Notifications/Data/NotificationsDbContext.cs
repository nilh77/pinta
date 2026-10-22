using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Notifications.Domain;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Notifications.Data;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public const string Schema = "notifications";

    public DbSet<OutgoingEmail> Outbox => Set<OutgoingEmail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<OutgoingEmail>(email =>
        {
            email.ToTable("outbox");
            email.Property(e => e.To).HasMaxLength(256);
            email.Property(e => e.Type).HasMaxLength(60);
            email.Property(e => e.Subject).HasMaxLength(300);
            email.Property(e => e.Status).HasSnakeCaseConversion(10);
            email.Property(e => e.LastError).HasMaxLength(1000);
            email.HasIndex(e => e.JobId);
        });
    }
}
