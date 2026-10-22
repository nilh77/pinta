namespace Pinta.Pricing.Tests;

/// <summary>The seed configuration of docs/pricing-engine.md ("Configuración semilla"); the golden cases use exactly these values.</summary>
internal static class SeedSnapshot
{
    public static readonly PriceSnapshot Value = new(
        Paints: new Dictionary<string, PaintRate>
        {
            ["matte"] = new(PricePerLitre: 8.00m, CoverageM2PerLitre: 10m, Coats: 2),
            ["satin"] = new(11.00m, 10m, 2),
            ["anti_damp"] = new(16.00m, 8m, 2),
            ["exterior"] = new(14.00m, 9m, 2),
        },
        SurfaceTypes: new Dictionary<string, SurfaceRate>
        {
            ["interior_wall"] = new(ClientRatePerM2: 6.00m, PainterRatePerM2: 4.00m, DifficultyFactor: 1.00m),
            ["ceiling"] = new(7.00m, 4.80m, 1.15m),
            ["facade"] = new(12.00m, 8.50m, 1.40m),
            ["wood"] = new(9.00m, 6.00m, 1.20m),
            ["metal"] = new(10.00m, 7.00m, 1.30m),
        },
        OccupancyFactors: new Dictionary<string, decimal>
        {
            ["empty"] = 1.00m,
            ["furnished"] = 1.10m,
            ["inhabited"] = 1.20m,
        },
        RoomSizes: new Dictionary<string, RoomSizeTable>
        {
            ["living"] = new(new(4m, 3m), new(5m, 4m), new(6m, 5m)),
            ["bedroom"] = new(new(3m, 2.5m), new(3.5m, 3m), new(4.5m, 4m)),
            ["kitchen"] = new(new(3m, 2m), new(3.5m, 3m), new(4.5m, 3.5m)),
            ["bathroom"] = new(new(2m, 1.5m), new(2.5m, 2m), new(3m, 2.5m)),
            ["hallway"] = new(new(3m, 1m), new(5m, 1.2m), new(7m, 1.2m)),
            ["other"] = new(new(2.5m, 2.5m), new(3m, 3m), new(4m, 3.5m)),
        },
        Rules: new PricingRules(
            WastePct: 10m,
            VatPct: 23m,
            MinJobGross: 150.00m,
            MaxJobGross: 15_000.00m,
            PremiumAreaM2: 250m,
            RangeLowPct: 10m,
            RangeHighPct: 15m,
            RangeSizeWideningPct: 10m,
            RangeDamageWideningPct: 10m,
            DepositPct: 30m,
            MinMarginPct: 20m,
            DefaultHeight: 2.60m,
            FullRefundMinDays: 7,
            LateRefundPct: 50m));
}
