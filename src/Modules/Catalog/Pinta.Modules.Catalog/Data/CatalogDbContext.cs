using Microsoft.EntityFrameworkCore;

namespace Pinta.Modules.Catalog.Data;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasDefaultSchema(Schema);
}
