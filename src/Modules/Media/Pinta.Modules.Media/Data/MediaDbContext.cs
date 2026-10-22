using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Media.Domain;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Media.Data;

public sealed class MediaDbContext(DbContextOptions<MediaDbContext> options) : DbContext(options)
{
    public const string Schema = "media";

    public DbSet<MediaFile> Files => Set<MediaFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<MediaFile>(file =>
        {
            file.ToTable("files");
            file.Property(f => f.ElementKey).HasMaxLength(64);
            file.Property(f => f.ContentType).HasMaxLength(64);
            file.Property(f => f.StorageKey).HasMaxLength(200);
            file.Property(f => f.Context).HasSnakeCaseConversion(20);
            file.Property(f => f.Kind).HasSnakeCaseConversion(10);
            file.HasIndex(f => new { f.DraftId, f.JobId });
            file.HasIndex(f => f.JobId);
        });
    }
}
