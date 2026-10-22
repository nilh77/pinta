namespace Pinta.Modules.Catalog.Domain;

public sealed class RoomType : CatalogEntity
{
    public required RoomSizes Sizes { get; set; }

    public Guid SuggestedPaintId { get; set; }
}

/// <summary>Length × width in metres of the S, M and L sizes, stored as <c>jsonb</c>.</summary>
public sealed class RoomSizes
{
    public required RoomMeasures S { get; set; }

    public required RoomMeasures M { get; set; }

    public required RoomMeasures L { get; set; }
}

public sealed class RoomMeasures
{
    public decimal Length { get; set; }

    public decimal Width { get; set; }
}
