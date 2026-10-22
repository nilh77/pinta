using Pinta.Pricing;

namespace Pinta.Modules.Quoting.Domain;

/// <summary>Serialized as <c>estimate</c>, <c>admin_review</c>, <c>site_visit</c>, <c>revision</c> (docs/api.md).</summary>
public enum QuoteOrigin
{
    Estimate,
    AdminReview,
    SiteVisit,
    Revision,
}

/// <summary>
/// One calculation of a job's price with the configuration frozen in <see cref="PriceSnapshot"/>, so changing rates
/// never alters a stored quote (docs/data-model.md). Amounts in euros before rounding to cents in the API.
/// </summary>
public sealed class QuoteVersion
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid JobId { get; init; }

    public int Number { get; init; }

    public QuoteOrigin Origin { get; init; }

    public required PriceSnapshot PriceSnapshot { get; init; }

    public required string Occupancy { get; init; }

    public decimal AreaM2 { get; set; }

    public decimal MaterialCost { get; set; }

    public decimal LabourPrice { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Vat { get; set; }

    public decimal Total { get; set; }

    public decimal PainterPay { get; set; }

    public decimal Margin { get; set; }

    public decimal? RangeMin { get; set; }

    public decimal? RangeMax { get; set; }

    public bool MinimumApplied { get; set; }

    public bool OverMax { get; set; }

    public bool Premium { get; set; }

    public decimal? ManualAdjustment { get; set; }

    public string? AdjustmentReason { get; set; }

    public DateOnly? ValidUntil { get; set; }

    public Guid CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public List<QuoteElement> Elements { get; init; } = [];

    public List<QuoteExtra> Extras { get; init; } = [];
}

/// <summary>A room or loose surface of a quote version (docs/data-model.md, ESTANCIA).</summary>
public sealed class QuoteElement
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid QuoteVersionId { get; init; }

    public int Position { get; init; }

    public string? ClientKey { get; init; }

    public string? Name { get; init; }

    public required string Kind { get; init; }

    public string? RoomType { get; init; }

    public string? SurfaceType { get; init; }

    public RoomSize? Size { get; init; }

    public decimal? Length { get; init; }

    public decimal? Width { get; init; }

    public decimal? Height { get; init; }

    public bool WallA { get; init; }

    public bool WallB { get; init; }

    public bool WallC { get; init; }

    public bool WallD { get; init; }

    public bool Ceiling { get; init; }

    public required string Paint { get; init; }

    public bool HasDamage { get; init; }

    public decimal AreaM2 { get; init; }
}

/// <summary>An extra item added by the admin (docs/data-model.md, LINEA_EXTRA); none in an estimate.</summary>
public sealed class QuoteExtra
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid QuoteVersionId { get; init; }

    public required string Description { get; init; }

    public decimal ClientPrice { get; init; }

    public decimal PainterPay { get; init; }
}
