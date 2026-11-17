using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Jobs.Domain;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Jobs.Data;

public sealed class JobsDbContext(DbContextOptions<JobsDbContext> options) : DbContext(options)
{
    public const string Schema = "jobs";

    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<Assignment> Assignments => Set<Assignment>();

    public DbSet<JobIncident> Incidents => Set<JobIncident>();

    public DbSet<JobActivity> Activity => Set<JobActivity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<JobActivity>(activity =>
        {
            activity.ToTable("job_activity");
            activity.HasKey(a => a.JobId);
            activity.Property(a => a.JobId).ValueGeneratedNever();
            activity.HasIndex(a => a.LastActivityAt);
            activity.HasOne<Job>().WithOne().HasForeignKey<JobActivity>(a => a.JobId);
        });
        modelBuilder.Entity<JobIncident>(incident =>
        {
            incident.ToTable("incidents");
            incident.Property(x => x.Id).ValueGeneratedNever();
            incident.Property(x => x.Text).HasMaxLength(2000);
            incident.HasIndex(x => x.JobId);
            incident.HasOne<Job>().WithMany().HasForeignKey(x => x.JobId);
        });
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
            job.Property(j => j.SiteVisitNotes).HasMaxLength(2000);
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

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await WithdrawStaleOffersAsync(cancellationToken);
        var activity = PendingActivity();
        var saved = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        foreach (var (jobId, at) in activity)
        {
            await RecordActivityAsync(jobId, at, cancellationToken);
        }

        return saved;
    }

    /// <summary>
    /// Marks the job as just worked on (docs/data-model.md, «Actividad del trabajo»). Saving a job, an offer, an
    /// answer or an incident records it by itself; call this for activity that changes nothing in this module
    /// (progress photos, the price draft, a client's question).
    /// </summary>
    public Task RecordActivityAsync(Guid jobId, DateTimeOffset at, CancellationToken cancellationToken) =>
        Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO jobs.job_activity (job_id, last_activity_at) VALUES ({jobId}, {at})
            ON CONFLICT (job_id) DO UPDATE SET last_activity_at = GREATEST(job_activity.last_activity_at, EXCLUDED.last_activity_at)
            """, cancellationToken);

    /// <summary>The jobs touched by the pending changes, with the moment of each change.</summary>
    private Dictionary<Guid, DateTimeOffset> PendingActivity()
    {
        var touched = new Dictionary<Guid, DateTimeOffset>();
        void Touch(Guid jobId, DateTimeOffset? at)
        {
            if (at is { } moment && (!touched.TryGetValue(jobId, out var known) || moment > known))
            {
                touched[jobId] = moment;
            }
        }

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            switch (entry.Entity)
            {
                case Job job:
                    Touch(job.Id, entry.State == EntityState.Added ? job.CreatedAt : job.UpdatedAt);
                    break;
                case Assignment assignment:
                    Touch(assignment.JobId, assignment.RespondedAt ?? assignment.OfferedAt);
                    break;
                case JobIncident incident:
                    Touch(incident.JobId, incident.CreatedAt);
                    break;
            }
        }

        return touched;
    }

    /// <summary>
    /// P1.2: an offer can only be accepted while the job has its deposit paid. When the job leaves that state by any path
    /// (scheduled by hand, cancelled, refunded), its open offers are withdrawn so the painter no longer sees them.
    /// </summary>
    private async Task WithdrawStaleOffersAsync(CancellationToken cancellationToken)
    {
        var left = ChangeTracker.Entries<Job>()
            .Where(e => e.State == EntityState.Modified
                && e.Property(j => j.Status).OriginalValue == JobStatus.DepositPaid
                && e.Entity.Status != JobStatus.DepositPaid)
            .Select(e => e.Entity)
            .ToList();
        foreach (var job in left)
        {
            var open = await Assignments.Where(a => a.JobId == job.Id && a.Status == AssignmentStatus.Offered).ToListAsync(cancellationToken);
            foreach (var offer in open.Where(o => o.Status == AssignmentStatus.Offered))
            {
                offer.Status = AssignmentStatus.Withdrawn;
                offer.RespondedAt = job.UpdatedAt;
            }
        }
    }
}

public sealed class JobCodeCounter
{
    public int Year { get; init; }

    public int Last { get; set; }
}
