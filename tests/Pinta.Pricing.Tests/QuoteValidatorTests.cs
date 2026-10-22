namespace Pinta.Pricing.Tests;

public class QuoteValidatorTests
{
    private static readonly Walls AllWalls = new(true, true, true, true);

    private static IReadOnlyList<QuoteError> Validate(params QuoteElement[] elements) =>
        QuoteValidator.Validate(SeedSnapshot.Value, new QuoteInput(elements, "empty"));

    private static Room Exact(decimal length, decimal width, decimal? height = null) =>
        new("bedroom", null, length, width, height, AllWalls, true, "matte", false);

    [Theory]
    [InlineData("0.50", "30.00", null)]
    [InlineData("4", "3", "2.00")]
    [InlineData("4", "3", "6.00")]
    public void Room_measures_at_the_bounds_are_valid(string length, string width, string? height) =>
        Assert.Empty(Validate(Exact(D(length), D(width), height is null ? null : D(height))));

    [Theory]
    [InlineData("0.49", "3", null, "elements[0].length")]
    [InlineData("4", "30.01", null, "elements[0].width")]
    [InlineData("4", "3", "1.99", "elements[0].height")]
    [InlineData("4", "3", "6.01", "elements[0].height")]
    public void Room_measures_out_of_bounds_are_rejected(string length, string width, string? height, string path) =>
        Assert.Equal([new QuoteError(path, QuoteErrorCodes.OutOfRange)],
            Validate(Exact(D(length), D(width), height is null ? null : D(height))));

    [Fact]
    public void Measures_with_more_than_two_decimals_are_rejected() =>
        Assert.Equal([new QuoteError("elements[0].length", QuoteErrorCodes.TooManyDecimals)], Validate(Exact(4.005m, 3m)));

    [Fact]
    public void Loose_surface_allows_up_to_40_by_20()
    {
        Assert.Empty(Validate(new LooseSurface("facade", 40m, 20m, "exterior", false)));
        Assert.Equal(
            [
                new QuoteError("elements[0].length", QuoteErrorCodes.OutOfRange),
                new QuoteError("elements[0].width", QuoteErrorCodes.OutOfRange),
            ],
            Validate(new LooseSurface("facade", 40.01m, 20.01m, "exterior", false)));
    }

    [Fact]
    public void Room_needs_a_size_or_both_measures_but_not_both()
    {
        var neither = new Room("bedroom", null, 4m, null, null, AllWalls, true, "matte", false);
        var both = new Room("bedroom", RoomSize.M, 4m, 3m, null, AllWalls, true, "matte", false);

        Assert.Equal([new QuoteError("elements[0].size", QuoteErrorCodes.SizeOrMeasuresRequired)], Validate(neither));
        Assert.Equal([new QuoteError("elements[0].size", QuoteErrorCodes.SizeOrMeasuresRequired)], Validate(both));
    }

    [Fact]
    public void Ceiling_only_room_is_valid() =>
        Assert.Empty(Validate(new Room("bedroom", null, 4m, 3m, null, new Walls(false, false, false, false), true, "matte", false)));

    [Fact]
    public void Between_one_and_thirty_elements()
    {
        Assert.Equal([new QuoteError("elements", QuoteErrorCodes.ElementCount)], Validate());
        Assert.Empty(Validate([.. Enumerable.Repeat(Exact(4m, 3m), 30)]));
        Assert.Equal([new QuoteError("elements", QuoteErrorCodes.ElementCount)], Validate([.. Enumerable.Repeat(Exact(4m, 3m), 31)]));
    }

    [Fact]
    public void Unknown_catalog_keys_are_rejected()
    {
        var input = new QuoteInput(
            [
                new Room("garage", null, 4m, 3m, null, AllWalls, true, "gloss", false),
                new LooseSurface("glass", 2m, 2m, "matte", false),
            ],
            "abandoned");

        Assert.Equal(
            [
                new QuoteError("occupancy", QuoteErrorCodes.UnknownOccupancy),
                new QuoteError("elements[0].paintId", QuoteErrorCodes.UnknownPaint),
                new QuoteError("elements[0].roomTypeId", QuoteErrorCodes.UnknownRoomType),
                new QuoteError("elements[1].surfaceTypeId", QuoteErrorCodes.UnknownSurfaceType),
            ],
            QuoteValidator.Validate(SeedSnapshot.Value, input));
    }

    [Fact]
    public void Rooms_need_the_wall_and_ceiling_surface_types_in_the_snapshot()
    {
        var snapshot = SeedSnapshot.Value with
        {
            SurfaceTypes = SeedSnapshot.Value.SurfaceTypes
                .Where(s => s.Key is not ("interior_wall" or "ceiling"))
                .ToDictionary(s => s.Key, s => s.Value),
        };

        Assert.Equal(
            [
                new QuoteError("elements[0].walls", QuoteErrorCodes.UnknownSurfaceType),
                new QuoteError("elements[0].ceiling", QuoteErrorCodes.UnknownSurfaceType),
            ],
            QuoteValidator.Validate(snapshot, new QuoteInput([Exact(4m, 3m)], "empty")));
    }

    [Fact]
    public void Extra_amounts_must_be_non_negative_with_two_decimals()
    {
        var input = new QuoteInput([Exact(4m, 3m)], "empty", [new ExtraLine(-1m, 10.005m)]);

        Assert.Equal(
            [
                new QuoteError("extras[0].clientPrice", QuoteErrorCodes.OutOfRange),
                new QuoteError("extras[0].painterPay", QuoteErrorCodes.TooManyDecimals),
            ],
            QuoteValidator.Validate(SeedSnapshot.Value, input));
    }

    private static decimal D(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
