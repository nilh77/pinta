using Microsoft.EntityFrameworkCore;

namespace Pinta.Modules.Painters.Data;

public sealed class PaintersDbContext(DbContextOptions<PaintersDbContext> options) : DbContext(options)
{
    public const string Schema = "painters";

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasDefaultSchema(Schema);
}
