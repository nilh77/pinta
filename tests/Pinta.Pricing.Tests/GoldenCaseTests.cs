namespace Pinta.Pricing.Tests;

/// <summary>
/// Golden cases TC01–TC13 of docs/pricing-engine.md, to the cent. Expected values come from
/// <c>python3 docs/reference/pricing_reference.py</c>; change that script first, never these numbers.
/// </summary>
public class GoldenCaseTests
{
    private static readonly Walls AllWalls = new(true, true, true, true);
    private static readonly Walls WallsAbc = new(true, true, true, false);

    private static Room ExactRoom(string type, string paint, decimal length, decimal width, decimal? height = null,
        bool ceiling = true, bool damage = false) =>
        new(type, Size: null, length, width, height, AllWalls, ceiling, paint, damage);

    private static Room SizedRoom(string type, string paint, RoomSize size, Walls? walls = null) =>
        new(type, size, Length: null, Width: null, Height: null, walls ?? AllWalls, Ceiling: true, paint, HasDamage: false);

    private static LooseSurface Surface(string type, string paint, decimal length, decimal width) =>
        new(type, length, width, paint, HasDamage: false);

    private static readonly QuoteElement[] Flat =
    [
        ExactRoom("living", "matte", 5m, 4m),
        ExactRoom("kitchen", "satin", 3.5m, 3m),
        ExactRoom("bedroom", "matte", 4m, 3.5m),
        SizedRoom("bedroom", "matte", RoomSize.M, WallsAbc),
        ExactRoom("bathroom", "anti_damp", 2.5m, 2m, damage: true),
        ExactRoom("hallway", "matte", 6m, 1.2m, ceiling: false),
    ];

    private static readonly Dictionary<string, QuoteInput> Inputs = new()
    {
        ["TC01"] = new([ExactRoom("bedroom", "matte", 4m, 3m)], "empty"),
        ["TC02"] = new([ExactRoom("bedroom", "matte", 4m, 3m)], "furnished"),
        ["TC03"] = new([ExactRoom("bedroom", "matte", 4m, 3m)], "inhabited"),
        ["TC04"] = new([SizedRoom("bedroom", "matte", RoomSize.M, WallsAbc)], "empty"),
        ["TC05"] = new([Surface("interior_wall", "matte", 3m, 2.6m)], "empty"),
        ["TC06"] = new([Surface("ceiling", "matte", 5m, 4m)], "empty"),
        ["TC07"] = new([ExactRoom("bathroom", "anti_damp", 2.5m, 2m, height: 2.4m, damage: true)], "empty"),
        ["TC08"] = new([Surface("facade", "exterior", 12m, 6m)], "empty"),
        ["TC09"] = new(Flat, "furnished"),
        ["TC10"] = new(Flat, "furnished", [new ExtraLine(ClientPrice: 121.95m, PainterPay: 75.00m)]),
        ["TC11"] = new(
            [.. Enumerable.Range(1, 8).Select(_ => SizedRoom("bedroom", "matte", RoomSize.L)), SizedRoom("living", "matte", RoomSize.L)],
            "empty"),
        ["TC12"] = new([Surface("facade", "exterior", 40m, 20m)], "empty"),
        ["TC14"] = new(Flat, "furnished", [new ExtraLine(ClientPrice: 121.95m, PainterPay: 75.00m)]) { ManualSubtotal = 2500.00m },
    };

    public sealed record Expected(
        decimal Area, decimal Litres, decimal Material, decimal Labour, bool MinimumApplied, decimal Subtotal,
        decimal Vat, decimal Total, decimal Painter, decimal Margin, decimal MarginPct, decimal? RangeMin,
        decimal? RangeMax, bool OverMax, bool Premium, decimal Deposit, decimal Balance, bool RangeWidened,
        bool BelowMinMargin);

    public static TheoryData<string, Expected> Cases => new()
    {
        { "TC01", new(48.40m, 10.65m, 85.20m, 315.00m, false, 400.20m, 92.05m, 492.25m, 211.84m, 103.16m, 25.8m, 440m, 570m, false, false, 147.68m, 344.57m, false, false) },
        { "TC02", new(48.40m, 10.65m, 85.20m, 346.50m, false, 431.70m, 99.29m, 530.99m, 233.02m, 113.48m, 26.3m, 475m, 615m, false, false, 159.30m, 371.69m, false, false) },
        { "TC03", new(48.40m, 10.65m, 85.20m, 378.00m, false, 463.20m, 106.54m, 569.74m, 254.21m, 123.79m, 26.7m, 510m, 660m, false, false, 170.92m, 398.82m, false, false) },
        { "TC04", new(36.50m, 8.03m, 64.24m, 240.53m, false, 304.77m, 70.10m, 374.87m, 161.96m, 78.57m, 25.8m, 335m, 470m, false, false, 112.46m, 262.41m, true, false) },
        { "TC05", new(7.80m, 1.72m, 13.76m, 46.80m, true, 121.95m, 28.05m, 150.00m, 31.20m, 76.99m, 63.1m, 135m, 175m, false, false, 45.00m, 105.00m, false, false) },
        { "TC06", new(20.00m, 4.40m, 35.20m, 161.00m, false, 196.20m, 45.13m, 241.33m, 110.40m, 50.60m, 25.8m, 215m, 280m, false, false, 72.40m, 168.93m, false, false) },
        { "TC07", new(26.60m, 7.32m, 117.12m, 169.85m, false, 286.97m, 66.00m, 352.97m, 114.00m, 55.85m, 19.5m, 315m, 445m, false, false, 105.89m, 247.08m, true, true) },
        { "TC08", new(72.00m, 17.60m, 246.40m, 1209.60m, false, 1456.00m, 334.88m, 1790.88m, 856.80m, 352.80m, 24.2m, 1610m, 2060m, false, false, 537.26m, 1253.62m, false, false) },
        { "TC09", new(266.44m, 60.20m, 573.41m, 1893.81m, false, 2467.22m, 567.46m, 3034.68m, 1272.67m, 621.14m, 25.2m, 2730m, 4100m, false, true, 910.40m, 2124.28m, true, false) },
        { "TC10", new(266.44m, 60.20m, 573.41m, 1893.81m, false, 2589.17m, 595.51m, 3184.68m, 1347.67m, 668.09m, 25.8m, 2865m, 4300m, false, true, 955.40m, 2229.28m, true, false) },
        { "TC11", new(584.80m, 128.62m, 1028.96m, 3865.50m, false, 4894.46m, 1125.73m, 6020.19m, 2603.68m, 1261.82m, 25.8m, 5415m, 7530m, false, true, 1806.06m, 4214.13m, true, false) },
        { "TC14", new(266.44m, 60.20m, 573.41m, 1893.81m, false, 2500.00m, 575.00m, 3075.00m, 1347.67m, 578.92m, 23.2m, 2765m, 4155m, false, true, 922.50m, 2152.50m, true, false) },
        { "TC12", new(800.00m, 195.56m, 2737.84m, 13440.00m, false, 16177.84m, 3720.90m, 19898.74m, 9520.00m, 3920.00m, 24.2m, null, null, true, true, 5969.62m, 13929.12m, false, false) },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Golden_case_matches_the_reference_to_the_cent(string id, Expected expected)
    {
        var input = Inputs[id];
        Assert.Empty(QuoteValidator.Validate(SeedSnapshot.Value, input));

        var result = PricingEngine.Calculate(SeedSnapshot.Value, input);

        var actual = new Expected(
            result.AreaM2, result.Litres, result.MaterialCost, result.LabourPrice, result.MinimumApplied,
            result.Subtotal, result.Vat, result.Total, result.PainterPay, result.Margin, result.MarginPct,
            result.Range?.Min, result.Range?.Max, result.OverMax, result.Premium, result.Deposit, result.Balance,
            result.RangeWidened, result.BelowMinMargin);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TC13_room_without_walls_or_ceiling_is_a_validation_error()
    {
        var input = new QuoteInput(
            [new Room("bedroom", null, 4m, 3m, null, new Walls(false, false, false, false), Ceiling: false, "matte", false)],
            "empty");

        var errors = QuoteValidator.Validate(SeedSnapshot.Value, input);

        var error = Assert.Single(errors);
        Assert.Equal(new QuoteError("elements[0].walls", QuoteErrorCodes.WallsOrCeilingRequired), error);
        Assert.Throws<ArgumentException>(() => PricingEngine.Calculate(SeedSnapshot.Value, input));
    }

    [Fact]
    public void TC01_lines_match_the_api_example()
    {
        var result = PricingEngine.Calculate(SeedSnapshot.Value, Inputs["TC01"]);

        Assert.Equal(
            [
                new PricedLine(0, "interior_wall", 36.40m, 8.01m, 64.08m, 218.40m, 145.60m),
                new PricedLine(0, "ceiling", 12.00m, 2.64m, 21.12m, 96.60m, 66.24m),
            ],
            result.Lines);
        Assert.Equal([new ElementArea(0, 36.40m, 12.00m, 48.40m)], result.Elements);
    }
}
