namespace Pinta.SharedKernel.Data;

/// <summary>
/// Audit columns of every table (docs/data-model.md) and the optimistic concurrency token. In PostgreSQL the token is
/// the <c>xmin</c> system column; the API exposes it as <c>version</c>.
/// </summary>
public abstract class AuditedEntity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public uint Version { get; private set; }

    public void Stamp(DateTimeOffset now, Guid? userId)
    {
        if (CreatedAt == default)
        {
            CreatedAt = now;
        }

        UpdatedAt = now;
        UpdatedBy = userId;
    }
}
