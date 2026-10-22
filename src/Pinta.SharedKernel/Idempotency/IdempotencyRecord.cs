using Microsoft.EntityFrameworkCore;

namespace Pinta.SharedKernel.Idempotency;

/// <summary>
/// A request made with an <c>Idempotency-Key</c> (docs/api.md): the first one stores its response; repeating the key
/// with the same body returns it again, with another body is <c>409 idempotency_conflict</c>.
/// </summary>
public sealed class IdempotencyRecord
{
    public Guid UserId { get; init; }

    public Guid Key { get; init; }

    public required string RequestHash { get; init; }

    /// <summary>0 while the first request is still running.</summary>
    public int StatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public string? Location { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options) : DbContext(options)
{
    public const string Schema = "shared";

    public DbSet<IdempotencyRecord> Records => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<IdempotencyRecord>(record =>
        {
            record.ToTable("idempotency_keys");
            record.HasKey(r => new { r.UserId, r.Key });
            record.Property(r => r.RequestHash).HasMaxLength(64);
        });
    }
}
