namespace Pinta.Pricing;

public sealed record QuoteError(string Path, string Code);

/// <summary>Stable codes of <see cref="QuoteError"/>; the API translates them.</summary>
public static class QuoteErrorCodes
{
    public const string ElementCount = "element_count";
    public const string OutOfRange = "out_of_range";
    public const string TooManyDecimals = "too_many_decimals";
    public const string WallsOrCeilingRequired = "walls_or_ceiling_required";
    public const string SizeOrMeasuresRequired = "size_or_measures_required";
    public const string UnknownPaint = "unknown_paint";
    public const string UnknownSurfaceType = "unknown_surface_type";
    public const string UnknownRoomType = "unknown_room_type";
    public const string UnknownOccupancy = "unknown_occupancy";
}

/// <summary>Input validations of docs/pricing-engine.md ("Validaciones"); paths use the API's JSON names.</summary>
public static class QuoteValidator
{
    public const int MaxElements = 30;
    public const decimal MinSide = 0.50m;
    public const decimal MaxRoomSide = 30.00m;
    public const decimal MinHeight = 2.00m;
    public const decimal MaxHeight = 6.00m;
    public const decimal MaxSurfaceLength = 40.00m;
    public const decimal MaxSurfaceWidth = 20.00m;

    public static IReadOnlyList<QuoteError> Validate(PriceSnapshot snapshot, QuoteInput input)
    {
        var errors = new List<QuoteError>();
        if (input.Elements.Count is 0 or > MaxElements)
        {
            errors.Add(new("elements", QuoteErrorCodes.ElementCount));
        }

        if (!snapshot.OccupancyFactors.ContainsKey(input.Occupancy))
        {
            errors.Add(new("occupancy", QuoteErrorCodes.UnknownOccupancy));
        }

        for (var i = 0; i < input.Elements.Count; i++)
        {
            var path = $"elements[{i}]";
            var element = input.Elements[i];
            if (!snapshot.Paints.ContainsKey(element.Paint))
            {
                errors.Add(new($"{path}.paintId", QuoteErrorCodes.UnknownPaint));
            }

            switch (element)
            {
                case Room room:
                    ValidateRoom(snapshot, room, path, errors);
                    break;
                case LooseSurface surface:
                    if (!snapshot.SurfaceTypes.ContainsKey(surface.SurfaceType))
                    {
                        errors.Add(new($"{path}.surfaceTypeId", QuoteErrorCodes.UnknownSurfaceType));
                    }

                    Measure(surface.Length, MinSide, MaxSurfaceLength, $"{path}.length", errors);
                    Measure(surface.Width, MinSide, MaxSurfaceWidth, $"{path}.width", errors);
                    break;
            }
        }

        if (input.ManualSubtotal is { } manual)
        {
            Amount(manual, "manualSubtotal", errors);
        }

        if (input.SiteVisitCredit != 0)
        {
            Amount(input.SiteVisitCredit, "siteVisitCredit", errors);
        }

        for (var i = 0; i < input.Extras.Count; i++)
        {
            Amount(input.Extras[i].ClientPrice, $"extras[{i}].clientPrice", errors);
            Amount(input.Extras[i].PainterPay, $"extras[{i}].painterPay", errors);
        }

        return errors;
    }

    private static void ValidateRoom(PriceSnapshot snapshot, Room room, string path, List<QuoteError> errors)
    {
        if (!snapshot.RoomSizes.ContainsKey(room.RoomType))
        {
            errors.Add(new($"{path}.roomTypeId", QuoteErrorCodes.UnknownRoomType));
        }

        if (!room.Walls.Any && !room.Ceiling)
        {
            errors.Add(new($"{path}.walls", QuoteErrorCodes.WallsOrCeilingRequired));
        }

        // Rooms are priced with the wall and ceiling surface types; a snapshot without them cannot price a room.
        if (room.Walls.Any && !snapshot.SurfaceTypes.ContainsKey(PricingEngine.WallsSurfaceType))
        {
            errors.Add(new($"{path}.walls", QuoteErrorCodes.UnknownSurfaceType));
        }

        if (room.Ceiling && !snapshot.SurfaceTypes.ContainsKey(PricingEngine.CeilingSurfaceType))
        {
            errors.Add(new($"{path}.ceiling", QuoteErrorCodes.UnknownSurfaceType));
        }

        var hasMeasures = room.Length is not null || room.Width is not null || room.Height is not null;
        if (room.Size is not null)
        {
            if (hasMeasures)
            {
                errors.Add(new($"{path}.size", QuoteErrorCodes.SizeOrMeasuresRequired));
            }

            return;
        }

        if (room.Length is null || room.Width is null)
        {
            errors.Add(new($"{path}.size", QuoteErrorCodes.SizeOrMeasuresRequired));
            return;
        }

        Measure(room.Length.Value, MinSide, MaxRoomSide, $"{path}.length", errors);
        Measure(room.Width.Value, MinSide, MaxRoomSide, $"{path}.width", errors);
        if (room.Height is { } height)
        {
            Measure(height, MinHeight, MaxHeight, $"{path}.height", errors);
        }
    }

    private static void Measure(decimal value, decimal min, decimal max, string path, List<QuoteError> errors)
    {
        if (value < min || value > max)
        {
            errors.Add(new(path, QuoteErrorCodes.OutOfRange));
        }
        else if (value.Scale > 2 && value != Math.Round(value, 2))
        {
            errors.Add(new(path, QuoteErrorCodes.TooManyDecimals));
        }
    }

    private static void Amount(decimal value, string path, List<QuoteError> errors)
    {
        if (value < 0)
        {
            errors.Add(new(path, QuoteErrorCodes.OutOfRange));
        }
        else if (value != Math.Round(value, 2))
        {
            errors.Add(new(path, QuoteErrorCodes.TooManyDecimals));
        }
    }
}
