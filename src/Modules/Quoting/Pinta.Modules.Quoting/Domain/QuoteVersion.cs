using Pinta.Modules.Quoting.Contracts;
using Pinta.Pricing;
using Pinta.SharedKernel.Text;

namespace Pinta.Modules.Quoting.Domain;

/// <summary>
/// One calculation of a job's price with the configuration frozen in <see cref="PriceSnapshot"/>, so changing rates
/// never alters a stored quote (docs/data-model.md). Amounts in euros before rounding to cents in the API.
/// </summary>
public sealed class QuoteVersion
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid JobId { get; init; }

    public int Number { get; init; }

    public QuoteOrigin Origin { get; set; }

    public required PriceSnapshot PriceSnapshot { get; init; }

    public required string Occupancy { get; set; }

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

    /// <summary>Premium marked or unmarked by hand by the admin; null follows the automatic rule.</summary>
    public bool? PremiumOverride { get; set; }

    /// <summary>Subtotal fixed by hand (ajuste_manual, rule "Ajuste manual" of docs/pricing-engine.md).</summary>
    public decimal? ManualAdjustment { get; set; }

    public string? AdjustmentReason { get; set; }

    /// <summary>A paid deductible site visit, VAT included, taken off the subtotal (rule "Visita técnica").</summary>
    public decimal SiteVisitCredit { get; set; }

    /// <summary>Last day the confirmed price can be accepted (Lisbon).</summary>
    public DateOnly? ValidUntil { get; set; }

    /// <summary>Null while it is the estimate or a review draft; set when the admin confirms the price.</summary>
    public DateTimeOffset? ConfirmedAt { get; set; }

    public Guid? ConfirmedBy { get; set; }

    /// <summary>The reviewer's note to the client.</summary>
    public string? Note { get; set; }

    public bool IsDraft => ConfirmedAt is null && Origin != QuoteOrigin.Estimate;

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

    /// <summary>Catalog key when the item comes from the catalog (PARTIDA_EXTRA); null for a free item.</summary>
    public string? CatalogKey { get; init; }

    /// <summary>Catalog name in pt/es/en, copied when the item is added; a free item only has <see cref="Description"/>.</summary>
    public LocalizedText? Name { get; init; }

    public required string Description { get; init; }

    public decimal ClientPrice { get; init; }

    public decimal PainterPay { get; init; }

    /// <summary>A free item a painter proposed at the site visit: the admin still has to set its client price (P3.1).</summary>
    public bool ClientPriceToSet { get; init; }
}
