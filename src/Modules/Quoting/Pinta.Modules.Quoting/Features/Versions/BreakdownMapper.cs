using Pinta.Modules.Quoting.Contracts;
using Pinta.Pricing;
using Pinta.SharedKernel;

namespace Pinta.Modules.Quoting.Features.Versions;

/// <summary>Engine results to API DTOs: euros to integer cents.</summary>
internal static class BreakdownMapper
{
    public static ElementAreaDto Area(ElementArea a) => new(a.Index, a.WallsAreaM2, a.CeilingAreaM2, a.AreaM2);

    public static QuoteBreakdownDto ToDto(QuoteBreakdown b, IReadOnlyList<ExtraLineDto> extras) => new(
        b.Lines.Select(l => new PricedLineDto(l.ElementIndex, l.SurfaceType, l.AreaM2, l.Litres,
            Money.ToCents(l.MaterialCost), Money.ToCents(l.LabourPrice), Money.ToCents(l.PainterPay))).ToArray(),
        extras,
        b.Elements.Select(Area).ToArray(),
        b.AreaM2,
        b.Litres,
        Money.ToCents(b.MaterialCost),
        Money.ToCents(b.LabourPrice),
        Money.ToCents(b.ExtrasPrice),
        Money.ToCents(b.Subtotal),
        Money.ToCents(b.Vat),
        Money.ToCents(b.Total),
        Money.ToCents(b.PainterPay),
        Money.ToCents(b.Margin),
        b.MarginPct,
        b.BelowMinMargin,
        Money.ToCents(b.Deposit),
        Money.ToCents(b.Balance),
        b.Range is null ? null : Money.ToCents(b.Range.Min),
        b.Range is null ? null : Money.ToCents(b.Range.Max),
        b.MinimumApplied,
        b.OverMax,
        b.Premium,
        b.RangeWidened);
}
