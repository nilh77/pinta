using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Jobs.Domain;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Jobs.Data;

public sealed class JobsDbContext(DbContextOptions<JobsDbContext> options) : DbContext(options)
{
    public const string Schema = "jobs";

    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<Assignment> Assignments => Set<Assignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Job>(job =>
        {
            job.ToTable("jobs");
            job.HasIndex(j => j.Code).IsUnique();
            job.HasIndex(j => new { j.ClientId, j.Status });
            job.HasIndex(j => new { j.Status, j.QuoteValidUntil });
            job.Property(j => j.Code).HasMaxLength(12);
            job.Property(j => j.Status).HasSnakeCaseConversion(20);
            job.Property(j => j.AddressLine1).HasMaxLength(200);
            job.Property(j => j.Floor).HasMaxLength(50);
            job.Property(j => j.PostalCode).HasMaxLength(8);
            job.Property(j => j.Locality).HasMaxLength(100);
            job.Property(j => j.Municipality).HasMaxLength(100);
            job.Property(j => j.AccessNotes).HasMaxLength(500);
            job.Property(j => j.Description).HasMaxLength(2000);
            job.Property(j => j.TaxId).HasMaxLength(9);
            job.Property(j => j.CompanyName).HasMaxLength(200);
            job.Property(j => j.SiteVisitPrice).HasPrecision(12, 2);
            job.Property(j => j.Version).IsRowVersion();
            job.HasMany(j => j.History).WithOne().HasForeignKey(c => c.JobId);
            job.Navigation(j => j.History).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_history");
        });
        modelBuilder.Entity<JobStatusChange>(change =>
        {
            change.ToTable("status_history");
            // Ids are UUID v7 made in code: a new row reached through Job.History must be inserted, not updated.
            change.Property(c => c.Id).ValueGeneratedNever();
            change.Property(c => c.From).HasConversion(
                s => s == null ? null : System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(s.Value.ToString()),
                s => s == null ? null : Enum.Parse<Pinta.Modules.Jobs.Contracts.JobStatus>(s.Replace("_", ""), true)).HasMaxLength(20);
            change.Property(c => c.To).HasSnakeCaseConversion(20);
            change.Property(c => c.Actor).HasSnakeCaseConversion(10);
            change.Property(c => c.Reason).HasMaxLength(500);
        });
        modelBuilder.Entity<Assignment>(assignment =>
        {
            assignment.ToTable("assignments");
            assignment.HasIndex(a => new { a.JobId, a.Status });
            assignment.HasIndex(a => new { a.PainterId, a.Status });
            assignment.Property(a => a.Status).HasSnakeCaseConversion(20);
            assignment.Property(a => a.PainterPay).HasPrecision(12, 2);
            assignment.Property(a => a.DeclineReason).HasMaxLength(500);
            assignment.Property(a => a.Version).IsRowVersion();
            assignment.HasOne<Job>().WithMany().HasForeignKey(a => a.JobId);
        });
        modelBuilder.Entity<JobCodeCounter>(counter =>
        {
            counter.ToTable("code_counters");
            counter.HasKey(c => c.Year);
            counter.Property(c => c.Year).ValueGeneratedNever();
        });
    }

    /// <summary>
    /// Next <c>PT-YYYY-NNNN</c>, one counter per year incremented atomically in PostgreSQL, so concurrent requests never
    /// share a code.
    /// </summary>
    public async Task<string> NextCodeAsync(int year, CancellationToken cancellationToken)
    {
        // INSERT … RETURNING cannot be composed over: read the single row on the client.
        var numbers = await Database.SqlQuery<int>($"""
            INSERT INTO jobs.code_counters (year, last) VALUES ({year}, 1)
            ON CONFLICT (year) DO UPDATE SET last = code_counters.last + 1
            RETURNING last AS "Value"
            """).ToListAsync(cancellationToken);
        var number = numbers.Single();
        return $"PT-{year}-{number:0000}";
    }
}

public sealed class JobCodeCounter
{
    public int Year { get; init; }

    public int Last { get; set; }
}
