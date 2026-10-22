using Pinta.SharedKernel;

namespace Pinta.Modules.Catalog.Domain;

/// <summary>A visitor outside the service area who wants to hear when Pinta reaches them (C4.1).</summary>
public sealed class WaitlistEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string Email { get; init; }

    public required string PostalCode { get; init; }

    public Locale Locale { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
