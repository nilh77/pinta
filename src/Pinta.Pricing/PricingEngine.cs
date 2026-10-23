namespace Pinta.Pricing;

/// <summary>
/// The pricing algorithm of docs/pricing-engine.md, reproducing docs/reference/pricing_reference.py to the cent.
/// Every intermediate value is rounded to 2 decimals away from zero; totals are sums of rounded lines.
/// </summary>
public static class PricingEngine
{
    public const string WallsSurfaceType = "interior_wall";
    public const string CeilingSurfaceType = "ceiling";

    public static QuoteBreakdown Calculate(PriceSnapshot snapshot, QuoteInput input)
    {
        var errors = QuoteValidator.Validate(snapshot, input);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                $"Invalid quote input: {string.Join(", ", errors.Select(e => $"{e.Path} {e.Code}"))}", nameof(input));
        }

        var rules = snapshot.Rules;
        var occupancy = snapshot.OccupancyFactors[input.Occupancy];
        var lines = new List<PricedLine>();
        var areas = new List<ElementArea>();
        for (var i = 0; i < input.Elements.Count; i++)
        {
            areas.Add(AddLines(snapshot, input.Elements[i], i, occupancy, lines));
        }

        var area = lines.Sum(l => l.AreaM2);
        var material = lines.Sum(l => l.MaterialCost);
        var labour = lines.Sum(l => l.LabourPrice);
        var extrasPrice = input.Extras.Sum(x => x.ClientPrice);
        var painter = lines.Sum(l => l.PainterPay) + input.Extras.Sum(x => x.PainterPay);

        var net = material + labour + extrasPrice;
        var minNet = R2(rules.MinJobGross / (1 + Pct(rules.VatPct)));
        var minimumApplied = input.ManualSubtotal is null && net < minNet;
        // A paid deductible site visit (VAT included) comes off the subtotal without its VAT; no minimum again.
        var subtotal = (input.ManualSubtotal ?? (minimumApplied ? minNet : net)) - R2(input.SiteVisitCredit / (1 + Pct(rules.VatPct)));
        if (subtotal <= 0)
        {
            throw new ArgumentException("The site visit credit leaves nothing to charge.", nameof(input));
        }

        var vat = R2(subtotal * Pct(rules.VatPct));
        var total = subtotal + vat;
        var margin = subtotal - material - painter;
        var marginPct = Math.Round(margin / subtotal * 100, 1, MidpointRounding.AwayFromZero);

        var sized = input.Elements.Any(e => e is Room { Size: not null });
        var damaged = input.Elements.Any(e => e.HasDamage);
        var highPct = rules.RangeHighPct
            + (sized ? rules.RangeSizeWideningPct : 0)
            + (damaged ? rules.RangeDamageWideningPct : 0);
        var overMax = total > rules.MaxJobGross;
        PriceRange? range = overMax
            ? null
            : new(Floor5(total * (1 - Pct(rules.RangeLowPct))), Ceiling5(total * (1 + Pct(highPct))));
        var deposit = R2(total * Pct(rules.DepositPct));

        return new QuoteBreakdown(
            lines,
            areas,
            input.Extras,
            area,
            lines.Sum(l => l.Litres),
            material,
            labour,
            extrasPrice,
            minimumApplied,
            subtotal,
            vat,
            total,
            painter,
            margin,
            marginPct,
            BelowMinMargin: marginPct < rules.MinMarginPct,
            range,
            RangeWidened: sized || damaged,
            overMax,
            Premium: area > rules.PremiumAreaM2,
            deposit,
            total - deposit);
    }

    private static ElementArea AddLines(PriceSnapshot snapshot, QuoteElement element, int index, decimal occupancy, List<PricedLine> lines)
    {
        if (element is LooseSurface surface)
        {
            var area = R2(surface.Length * surface.Width);
            lines.Add(Line(snapshot, index, area, surface.Paint, surface.SurfaceType, occupancy));
            return new(index, null, null, area);
        }

        var room = (Room)element;
        decimal length, width, height;
        if (room.Size is { } size)
        {
            (length, width) = snapshot.RoomSizes[room.RoomType][size];
            height = snapshot.Rules.DefaultHeight;
        }
        else
        {
            (length, width, height) = (room.Length!.Value, room.Width!.Value, room.Height ?? snapshot.Rules.DefaultHeight);
        }

        var wallLength = (room.Walls.A ? length : 0) + (room.Walls.B ? width : 0)
            + (room.Walls.C ? length : 0) + (room.Walls.D ? width : 0);
        decimal? wallsArea = null, ceilingArea = null;
        if (wallLength > 0)
        {
            wallsArea = R2(wallLength * height);
            lines.Add(Line(snapshot, index, wallsArea.Value, room.Paint, WallsSurfaceType, occupancy));
        }

        if (room.Ceiling)
        {
            ceilingArea = R2(length * width);
            lines.Add(Line(snapshot, index, ceilingArea.Value, room.Paint, CeilingSurfaceType, occupancy));
        }

        return new(index, wallsArea ?? 0, ceilingArea ?? 0, (wallsArea ?? 0) + (ceilingArea ?? 0));
    }

    private static PricedLine Line(PriceSnapshot snapshot, int index, decimal area, string paintKey, string surfaceKey, decimal occupancy)
    {
        var paint = snapshot.Paints[paintKey];
        var surface = snapshot.SurfaceTypes[surfaceKey];
        var litres = R2(area * paint.Coats / paint.CoverageM2PerLitre * (1 + Pct(snapshot.Rules.WastePct)));
        return new PricedLine(
            index,
            surfaceKey,
            area,
            litres,
            R2(litres * paint.PricePerLitre),
            R2(area * surface.ClientRatePerM2 * surface.DifficultyFactor * occupancy),
            R2(area * surface.PainterRatePerM2 * surface.DifficultyFactor * occupancy));
    }

    internal static decimal R2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Pct(decimal percent) => percent / 100;

    private static decimal Floor5(decimal value) => Math.Floor(value / 5) * 5;

    private static decimal Ceiling5(decimal value) => Math.Ceiling(value / 5) * 5;
}
