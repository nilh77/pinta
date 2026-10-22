namespace Pinta.Modules.Catalog.Domain;

/// <summary>Factor applied to labour and painter pay. In the MVP the only group is occupancy.</summary>
public sealed class Modifier : CatalogEntity
{
    public const string OccupancyGroup = "occupancy";

    public string Group { get; init; } = OccupancyGroup;

    public decimal Factor { get; set; }
}
