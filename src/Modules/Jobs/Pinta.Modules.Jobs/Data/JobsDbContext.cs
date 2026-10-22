using Microsoft.EntityFrameworkCore;

namespace Pinta.Modules.Jobs.Data;

public sealed class JobsDbContext(DbContextOptions<JobsDbContext> options) : DbContext(options)
{
    public const string Schema = "jobs";

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasDefaultSchema(Schema);
}
