namespace Pinta.Modules.Painters.Contracts;

/// <summary>Serialized as <c>invited</c>, <c>active</c>, <c>inactive</c> (A4.2).</summary>
public enum PainterStatus
{
    Invited,
    Active,
    Inactive,
}

/// <summary>What other modules may know about a painter: never the IBAN.</summary>
public sealed record PainterSummary(
    Guid Id, Guid UserId, string Name, string Email, string? Phone, PainterStatus Status, IReadOnlyList<string> Municipalities, decimal? Rating);

public interface IPainterDirectory
{
    Task<PainterSummary?> FindAsync(Guid painterId, CancellationToken cancellationToken = default);

    Task<PainterSummary?> FindByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PainterSummary>> FindManyAsync(IReadOnlyCollection<Guid> painterIds, CancellationToken cancellationToken = default);

    /// <summary>Active painters whose zones include <paramref name="municipality"/> (A3.2).</summary>
    Task<IReadOnlyList<PainterSummary>> CoveringAsync(string municipality, CancellationToken cancellationToken = default);
}
