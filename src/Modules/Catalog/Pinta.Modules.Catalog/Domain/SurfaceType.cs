namespace Pinta.Modules.Catalog.Domain;

public sealed class SurfaceType : CatalogEntity
{
    /// <summary>Surface types the pricing engine uses for room walls and ceilings; they cannot be deactivated.</summary>
    public static readonly string[] RoomSurfaceKeys = [Pricing.PricingEngine.WallsSurfaceType, Pricing.PricingEngine.CeilingSurfaceType];

    public decimal ClientRatePerM2 { get; set; }

    public decimal PainterRatePerM2 { get; set; }

    public decimal DifficultyFactor { get; set; }
}
