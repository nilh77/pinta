using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Catalog.Domain;

/// <summary>An active 4-digit postal code prefix and its concelho (municipality).</summary>
public sealed class CoverageArea : AuditedEntity
{
    public required string PostalPrefix { get; init; }

    public required string Municipality { get; set; }

    public bool IsActive { get; set; } = true;
}
