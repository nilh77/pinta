namespace Pinta.Pricing;

/// <summary>
/// Frozen pricing configuration (<c>PriceSnapshot</c>, docs/glossary.md). A quote version stores it, so recalculating
/// an old quote always gives the same numbers. Catalog items are keyed by their stable catalog key.
/// </summary>
public sealed record PriceSnapshot(
    IReadOnlyDictionary<string, PaintRate> Paints,
    IReadOnlyDictionary<string, SurfaceRate> SurfaceTypes,
    IReadOnlyDictionary<string, decimal> OccupancyFactors,
    IReadOnlyDictionary<string, RoomSizeTable> RoomSizes,
    PricingRules Rules);

/// <summary>Paint: euros per litre, m² one litre covers per coat, and number of coats.</summary>
public sealed record PaintRate(decimal PricePerLitre, decimal CoverageM2PerLitre, int Coats);

/// <summary>Surface type: client labour rate and painter pay per m² (before VAT) and difficulty factor.</summary>
public sealed record SurfaceRate(decimal ClientRatePerM2, decimal PainterRatePerM2, decimal DifficultyFactor);

/// <summary>Length × width in metres of a room type's S, M and L sizes; the height is the default height.</summary>
public sealed record RoomSizeTable(RoomDimensions S, RoomDimensions M, RoomDimensions L)
{
    public RoomDimensions this[RoomSize size] => size switch
    {
        RoomSize.S => S,
        RoomSize.M => M,
        RoomSize.L => L,
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };
}

public sealed record RoomDimensions(decimal Length, decimal Width);

/// <summary>Global rules of docs/pricing-engine.md. Percentages are 0–100; amounts in euros with VAT where stated.</summary>
public sealed record PricingRules(
    decimal WastePct,
    decimal VatPct,
    decimal MinJobGross,
    decimal MaxJobGross,
    decimal PremiumAreaM2,
    decimal RangeLowPct,
    decimal RangeHighPct,
    decimal RangeSizeWideningPct,
    decimal RangeDamageWideningPct,
    decimal DepositPct,
    decimal MinMarginPct,
    decimal DefaultHeight,
    int FullRefundMinDays,
    decimal LateRefundPct);
