using Pinta.SharedKernel.Data;
using Pinta.SharedKernel.Text;

namespace Pinta.Modules.Catalog.Domain;

/// <summary>
/// A catalog item: stable key, translatable name and active flag. Items are never deleted, only deactivated, because
/// existing quotes keep referencing them (docs/data-model.md).
/// </summary>
public abstract class CatalogEntity : AuditedEntity
{
    public required string Key { get; init; }

    public required LocalizedText Name { get; set; }

    public bool IsActive { get; set; } = true;
}
