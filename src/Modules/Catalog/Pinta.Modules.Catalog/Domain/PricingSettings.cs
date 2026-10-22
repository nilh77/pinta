using Pinta.Pricing;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Catalog.Domain;

/// <summary>
/// Global configuration (one row, A1.6): the rules of docs/pricing-engine.md plus the operational parameters of the
/// data model. Percentages are 0–100; job amounts include VAT.
/// </summary>
public sealed class PricingSettings : AuditedEntity
{
    public decimal WastePct { get; set; }
    public decimal VatPct { get; set; }
    public decimal MinJobAmount { get; set; }
    public decimal MaxJobAmount { get; set; }
    public decimal PremiumAreaM2 { get; set; }
    public decimal RangeLowPct { get; set; }
    public decimal RangeHighPct { get; set; }
    public decimal RangeSizeWideningPct { get; set; }
    public decimal RangeDamageWideningPct { get; set; }
    public decimal DepositPct { get; set; }
    public decimal MinMarginPct { get; set; }
    public decimal DefaultHeight { get; set; }
    public int QuoteValidityDays { get; set; }
    /// <summary>Preferred dates start at least this many working days after today (C4.2).</summary>
    public int MinLeadBusinessDays { get; set; } = 3;
    public decimal SiteVisitPrice { get; set; }
    public bool SiteVisitDeductible { get; set; }
    public int ReviewDeadlineBusinessDays { get; set; }
    public int FullRefundMinDays { get; set; }
    public decimal LateRefundPct { get; set; }
    public int MaxPhotosPerElement { get; set; }
    public int MaxVideosPerElement { get; set; }
    public int MaxImageSizeMb { get; set; }
    public int MaxVideoSizeMb { get; set; }
    public bool ClientPhoneVisibleToPainter { get; set; }
    public bool PainterPhoneVisibleToClient { get; set; }

    public PricingRules ToRules() => new(
        WastePct,
        VatPct,
        MinJobAmount,
        MaxJobAmount,
        PremiumAreaM2,
        RangeLowPct,
        RangeHighPct,
        RangeSizeWideningPct,
        RangeDamageWideningPct,
        DepositPct,
        MinMarginPct,
        DefaultHeight,
        FullRefundMinDays,
        LateRefundPct);
}
