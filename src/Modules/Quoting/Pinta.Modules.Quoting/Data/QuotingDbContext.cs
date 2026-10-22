using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Quoting.Domain;
using PriceSnapshot = Pinta.Pricing.PriceSnapshot;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Quoting.Data;

public sealed class QuotingDbContext(DbContextOptions<QuotingDbContext> options) : DbContext(options)
{
    public const string Schema = "quoting";

    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);

    public DbSet<QuoteVersion> Versions => Set<QuoteVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<QuoteVersion>(version =>
        {
            version.ToTable("quote_versions");
            version.HasIndex(v => new { v.JobId, v.Number }).IsUnique();
            version.Property(v => v.Origin).HasSnakeCaseConversion(20);
            version.Property(v => v.Occupancy).HasMaxLength(40);
            // The frozen configuration (snapshot_precios) is the engine's own input type, stored as jsonb.
            version.Property(v => v.PriceSnapshot)
                .HasColumnName("price_snapshot")
                .HasColumnType("jsonb")
                .HasConversion(
                    snapshot => JsonSerializer.Serialize(snapshot, SnapshotJson),
                    json => JsonSerializer.Deserialize<PriceSnapshot>(json, SnapshotJson)!);
            version.Property(v => v.AdjustmentReason).HasMaxLength(500);
            foreach (var property in version.Metadata.GetProperties().Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetPrecision(12);
                property.SetScale(2);
            }

            version.HasMany(v => v.Elements).WithOne().HasForeignKey(e => e.QuoteVersionId);
            version.HasMany(v => v.Extras).WithOne().HasForeignKey(e => e.QuoteVersionId);
        });
        modelBuilder.Entity<QuoteElement>(element =>
        {
            element.ToTable("quote_elements");
            element.Property(e => e.ClientKey).HasMaxLength(64);
            element.Property(e => e.Name).HasMaxLength(80);
            element.Property(e => e.Kind).HasMaxLength(10);
            element.Property(e => e.RoomType).HasMaxLength(40);
            element.Property(e => e.SurfaceType).HasMaxLength(40);
            element.Property(e => e.Paint).HasMaxLength(40);
            element.Property(e => e.Size).HasConversion<string>().HasMaxLength(1);
            element.Property(e => e.Length).HasPrecision(6, 2);
            element.Property(e => e.Width).HasPrecision(6, 2);
            element.Property(e => e.Height).HasPrecision(6, 2);
            element.Property(e => e.AreaM2).HasPrecision(12, 2);
        });
        modelBuilder.Entity<QuoteExtra>(extra =>
        {
            extra.ToTable("quote_extras");
            extra.Property(x => x.Description).HasMaxLength(200);
            extra.Property(x => x.ClientPrice).HasPrecision(12, 2);
            extra.Property(x => x.PainterPay).HasPrecision(12, 2);
        });
    }
}
