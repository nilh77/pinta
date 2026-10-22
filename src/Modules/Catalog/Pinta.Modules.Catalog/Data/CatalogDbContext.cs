using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pinta.Modules.Catalog.Domain;

namespace Pinta.Modules.Catalog.Data;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";

    public DbSet<Paint> Paints => Set<Paint>();
    public DbSet<SurfaceType> SurfaceTypes => Set<SurfaceType>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<Modifier> Modifiers => Set<Modifier>();
    public DbSet<ExtraItem> ExtraItems => Set<ExtraItem>();
    public DbSet<PricingSettings> Settings => Set<PricingSettings>();
    public DbSet<CoverageArea> CoverageAreas => Set<CoverageArea>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Paint>(paint =>
        {
            CatalogItem(paint, "paints");
            paint.Property(p => p.PricePerLitre).HasPrecision(12, 2);
            paint.Property(p => p.CoverageM2PerLitre).HasPrecision(12, 2);
        });
        modelBuilder.Entity<SurfaceType>(surface =>
        {
            CatalogItem(surface, "surface_types");
            surface.Property(s => s.ClientRatePerM2).HasPrecision(12, 2);
            surface.Property(s => s.PainterRatePerM2).HasPrecision(12, 2);
            surface.Property(s => s.DifficultyFactor).HasPrecision(5, 2);
        });
        modelBuilder.Entity<RoomType>(room =>
        {
            CatalogItem(room, "room_types");
            room.OwnsOne(r => r.Sizes, sizes =>
            {
                sizes.ToJson();
                sizes.OwnsOne(s => s.S, Measures("S"));
                sizes.OwnsOne(s => s.M, Measures("M"));
                sizes.OwnsOne(s => s.L, Measures("L"));
            });
            room.HasOne<Paint>().WithMany().HasForeignKey(r => r.SuggestedPaintId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Modifier>(modifier =>
        {
            CatalogItem(modifier, "modifiers", unique: false);
            modifier.Property(m => m.Group).HasMaxLength(40);
            modifier.HasIndex(m => new { m.Group, m.Key }).IsUnique();
            modifier.Property(m => m.Factor).HasPrecision(5, 2);
        });
        modelBuilder.Entity<ExtraItem>(extra =>
        {
            CatalogItem(extra, "extra_items");
            extra.Property(x => x.ClientPrice).HasPrecision(12, 2);
            extra.Property(x => x.PainterPay).HasPrecision(12, 2);
        });
        modelBuilder.Entity<PricingSettings>(settings =>
        {
            settings.ToTable("settings");
            settings.Property(s => s.Version).IsRowVersion();
            foreach (var property in settings.Metadata.GetProperties().Where(p => p.ClrType == typeof(decimal)))
            {
                property.SetPrecision(12);
                property.SetScale(2);
            }
        });
        modelBuilder.Entity<CoverageArea>(area =>
        {
            area.ToTable("coverage_areas");
            area.Property(a => a.Version).IsRowVersion();
            area.Property(a => a.PostalPrefix).HasMaxLength(4).IsFixedLength();
            area.HasIndex(a => a.PostalPrefix).IsUnique();
            area.Property(a => a.Municipality).HasMaxLength(100);
        });
    }

    private static void CatalogItem<T>(EntityTypeBuilder<T> entity, string table, bool unique = true)
        where T : CatalogEntity
    {
        entity.ToTable(table);
        entity.Property(e => e.Version).IsRowVersion();
        entity.Property(e => e.Key).HasMaxLength(40);
        if (unique)
        {
            entity.HasIndex(e => e.Key).IsUnique();
        }

        entity.OwnsOne(e => e.Name, name =>
        {
            name.ToJson();
            name.Property(n => n.Pt).HasJsonPropertyName("pt");
            name.Property(n => n.Es).HasJsonPropertyName("es");
            name.Property(n => n.En).HasJsonPropertyName("en");
        });
    }

    private static Action<OwnedNavigationBuilder<RoomSizes, RoomMeasures>> Measures(string size) => measures =>
    {
        measures.HasJsonPropertyName(size);
        measures.Property(m => m.Length).HasJsonPropertyName("length");
        measures.Property(m => m.Width).HasJsonPropertyName("width");
    };
}
