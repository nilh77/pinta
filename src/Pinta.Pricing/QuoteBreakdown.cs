namespace Pinta.Pricing;

/// <summary>Result of <see cref="PricingEngine.Calculate"/>. Amounts in euros, rounded to the cent.</summary>
public sealed record QuoteBreakdown(
    IReadOnlyList<PricedLine> Lines,
    IReadOnlyList<ElementArea> Elements,
    IReadOnlyList<ExtraLine> Extras,
    decimal AreaM2,
    decimal Litres,
    decimal MaterialCost,
    decimal LabourPrice,
    decimal ExtrasPrice,
    bool MinimumApplied,
    decimal Subtotal,
    decimal Vat,
    decimal Total,
    decimal PainterPay,
    decimal Margin,
    decimal MarginPct,
    bool BelowMinMargin,
    PriceRange? Range,
    bool RangeWidened,
    bool OverMax,
    bool Premium,
    decimal Deposit,
    decimal Balance);

/// <summary>One surface line of an element: a room gives a walls line and a ceiling line; a loose surface gives one.</summary>
public sealed record PricedLine(
    int ElementIndex,
    string SurfaceType,
    decimal AreaM2,
    decimal Litres,
    decimal MaterialCost,
    decimal LabourPrice,
    decimal PainterPay);

/// <summary>Areas of one element; walls and ceiling only for rooms.</summary>
public sealed record ElementArea(int Index, decimal? WallsAreaM2, decimal? CeilingAreaM2, decimal AreaM2);

/// <summary>Price range shown to the client, with VAT, in multiples of 5 €.</summary>
public sealed record PriceRange(decimal Min, decimal Max);
