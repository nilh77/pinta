using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Notifications.Domain;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Notifications.Data;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public const string Schema = "notifications";

    public DbSet<OutgoingEmail> Outbox => Set<OutgoingEmail>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Notification>(notification =>
        {
            notification.ToTable("notifications");
            notification.Property(n => n.Id).ValueGeneratedNever();
            notification.Property(n => n.Kind).HasSnakeCaseConversion(40);
            notification.Property(n => n.Audience).HasSnakeCaseConversion(10);
            notification.Property(n => n.JobCode).HasMaxLength(12);
            notification.Property(n => n.Data)
                .HasColumnType("jsonb")
                .HasConversion(
                    data => System.Text.Json.JsonSerializer.Serialize(data, (System.Text.Json.JsonSerializerOptions?)null),
                    json => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json, (System.Text.Json.JsonSerializerOptions?)null) ?? new())
                .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<string, string>>(
                    (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
                    d => d.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
                    d => new Dictionary<string, string>(d)));
            notification.HasIndex(n => new { n.UserId, n.ReadAt, n.CreatedAt });
        });
        modelBuilder.Entity<PushSubscription>(subscription =>
        {
            subscription.ToTable("push_subscriptions");
            subscription.Property(s => s.Id).ValueGeneratedNever();
            subscription.Property(s => s.Endpoint).HasMaxLength(1000);
            subscription.Property(s => s.P256dh).HasMaxLength(200);
            subscription.Property(s => s.Auth).HasMaxLength(100);
            subscription.Property(s => s.UserAgent).HasMaxLength(300);
            subscription.HasIndex(s => s.Endpoint).IsUnique();
            subscription.HasIndex(s => s.UserId);
        });
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
