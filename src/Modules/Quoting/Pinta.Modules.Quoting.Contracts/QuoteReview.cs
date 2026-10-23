using Pinta.Pricing;

namespace Pinta.Modules.Quoting.Contracts;

/// <summary>Serialized as <c>estimate</c>, <c>admin_review</c>, <c>site_visit</c>, <c>revision</c> (docs/api.md).</summary>
public enum QuoteOrigin
{
    Estimate,
    AdminReview,
    SiteVisit,
    Revision,
}

public sealed record PricedLineDto(
    int Element, string Surface, decimal AreaM2, decimal Litres, long MaterialCents, long LabourCents, long PainterCents);

public sealed record ExtraLineDto(string Description, long ClientPriceCents, long PainterPayCents);

public sealed record ElementAreaDto(int Index, decimal? WallsAreaM2, decimal? CeilingAreaM2, decimal AreaM2);

/// <summary>Full internal breakdown (admin only, docs/api.md "Desglose del admin").</summary>
public sealed record QuoteBreakdownDto(
    IReadOnlyList<PricedLineDto> Lines,
    IReadOnlyList<ExtraLineDto> Extras,
    IReadOnlyList<ElementAreaDto> Elements,
    decimal AreaM2,
    decimal Litres,
    long MaterialCents,
    long LabourCents,
    long ExtrasCents,
    long SubtotalCents,
    long VatCents,
    long TotalCents,
    long PainterTotalCents,
    long MarginCents,
    decimal MarginPct,
    bool BelowMinMargin,
    long DepositCents,
    long BalanceCents,
    long? RangeMinCents,
    long? RangeMaxCents,
    bool MinimumApplied,
    bool OverMax,
    bool Premium,
    bool RangeWidened);

/// <summary>An extra item of a review draft: from the catalog by key (its prices unless given) or free.</summary>
public sealed record DraftExtraRequest(string? CatalogKey, string? Description, long? ClientPriceCents, long? PainterPayCents);

/// <summary>
/// Body of <c>PUT /api/admin/jobs/{id}/draft-quote</c> (A2.3–A2.5, A2.7): the whole draft. <see cref="Premium"/> null
/// follows the automatic rule; <see cref="ManualSubtotalCents"/> needs <see cref="AdjustmentReason"/>.
/// </summary>
public sealed record DraftQuoteRequest(
    Occupancy Occupancy,
    IReadOnlyList<QuoteElementRequest> Elements,
    IReadOnlyList<DraftExtraRequest>? Extras,
    bool? Premium,
    long? ManualSubtotalCents,
    string? AdjustmentReason);

/// <summary>A stored element, with the same fields as <see cref="QuoteElementRequest"/> plus its id and area.</summary>
public sealed record QuoteElementView(
    Guid Id,
    ElementKind Kind,
    string? Name,
    string? RoomTypeId,
    RoomSize? Size,
    decimal? Length,
    decimal? Width,
    decimal? Height,
    bool[]? Walls,
    bool Ceiling,
    string? SurfaceTypeId,
    string PaintId,
    bool Damage,
    string? ClientKey,
    decimal AreaM2);

public sealed record QuoteExtraView(string? CatalogKey, string Description, long ClientPriceCents, long PainterPayCents);

/// <summary>A quote version as the admin sees it: what was priced and the full internal breakdown.</summary>
public sealed record AdminQuote(
    Guid Id,
    int Number,
    QuoteOrigin Origin,
    bool IsDraft,
    Occupancy Occupancy,
    IReadOnlyList<QuoteElementView> Elements,
    IReadOnlyList<QuoteExtraView> Extras,
    bool? PremiumOverride,
    long? ManualSubtotalCents,
    string? AdjustmentReason,
    long SiteVisitCreditCents,
    string? Note,
    DateOnly? ValidUntil,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    QuoteBreakdownDto Breakdown);

/// <summary>Serialized as <c>work</c>, <c>extra</c>, <c>site_visit</c> (the paid visit taken off, a negative amount).</summary>
public enum ClientQuoteLineKind
{
    Work,
    Extra,
    SiteVisit,
}

/// <summary>A line of the client's breakdown, VAT included: the painting work or one extra item.</summary>
public sealed record ClientQuoteLine(ClientQuoteLineKind Kind, string? Description, int? ElementCount, decimal? AreaM2, long AmountCents);

/// <summary>
/// The price as the client sees it (screen 10, docs/pricing-engine.md 6.3): lines with VAT, total, deposit and balance.
/// Never material, rates, painter pay or margin.
/// </summary>
public sealed record ClientQuote(
    Guid Id,
    int Number,
    IReadOnlyList<ClientQuoteLine> Lines,
    decimal AreaM2,
    long TotalCents,
    long VatCents,
    long DepositCents,
    long BalanceCents,
    bool Premium,
    DateOnly? ValidUntil,
    string? Note,
    DateTimeOffset? ConfirmedAt);

public sealed record QuoteVersionListItem(
    Guid Id,
    int Number,
    QuoteOrigin Origin,
    bool IsDraft,
    decimal AreaM2,
    long TotalCents,
    bool ManualAdjustment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    DateOnly? ValidUntil);

/// <summary>A confirmed price: the version to show the client and what goes in the email.</summary>
public sealed record ConfirmedQuote(Guid Id, long TotalCents, long DepositCents, DateOnly ValidUntil);
