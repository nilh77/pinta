using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Painters.Domain;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Painters.Data;

public sealed class PaintersDbContext(DbContextOptions<PaintersDbContext> options) : DbContext(options)
{
    public const string Schema = "painters";

    public DbSet<Painter> Painters => Set<Painter>();

    public DbSet<PainterDocument> Documents => Set<PainterDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Painter>(painter =>
        {
            painter.ToTable("painters");
            painter.HasIndex(p => p.UserId).IsUnique();
            painter.HasIndex(p => p.Email).IsUnique();
            painter.Property(p => p.Name).HasMaxLength(200);
            painter.Property(p => p.Email).HasMaxLength(256);
            painter.Property(p => p.Phone).HasMaxLength(20);
            painter.Property(p => p.TaxId).HasMaxLength(9);
            painter.Property(p => p.IbanEncrypted).HasMaxLength(1000);
            painter.Property(p => p.IbanLast4).HasMaxLength(4);
            painter.Property(p => p.Notes).HasMaxLength(2000);
            painter.Property(p => p.Rating).HasPrecision(2, 1);
            painter.Property(p => p.Status).HasSnakeCaseConversion(20);
            painter.Property(p => p.Version).IsRowVersion();
            painter.HasMany(p => p.Documents).WithOne().HasForeignKey(d => d.PainterId);
        });
        modelBuilder.Entity<PainterDocument>(document =>
        {
            document.ToTable("documents");
            document.Property(d => d.Id).ValueGeneratedNever();
            document.Property(d => d.Kind).HasSnakeCaseConversion(20);
        });
    }
}
