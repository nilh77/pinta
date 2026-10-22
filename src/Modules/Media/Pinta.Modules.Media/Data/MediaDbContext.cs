using Microsoft.EntityFrameworkCore;

namespace Pinta.Modules.Media.Data;

public sealed class MediaDbContext(DbContextOptions<MediaDbContext> options) : DbContext(options)
{
    public const string Schema = "media";

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasDefaultSchema(Schema);
}
