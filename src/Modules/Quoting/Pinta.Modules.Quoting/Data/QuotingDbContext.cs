using Microsoft.EntityFrameworkCore;

namespace Pinta.Modules.Quoting.Data;

public sealed class QuotingDbContext(DbContextOptions<QuotingDbContext> options) : DbContext(options)
{
    public const string Schema = "quoting";

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasDefaultSchema(Schema);
}
