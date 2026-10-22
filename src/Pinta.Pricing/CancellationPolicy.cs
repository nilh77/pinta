namespace Pinta.Pricing;

/// <summary>Deposit refund on cancellation (docs/pricing-engine.md, "Reembolso por cancelación").</summary>
public static class CancellationPolicy
{
    /// <summary>
    /// Full deposit when there is no scheduled date or at least <see cref="PricingRules.FullRefundMinDays"/> days remain;
    /// otherwise <see cref="PricingRules.LateRefundPct"/> of it. The caller counts the days in Europe/Lisbon.
    /// </summary>
    public static decimal Refund(PricingRules rules, decimal deposit, int? daysBeforeScheduledDate) =>
        daysBeforeScheduledDate is null || daysBeforeScheduledDate >= rules.FullRefundMinDays
            ? deposit
            : PricingEngine.R2(deposit * rules.LateRefundPct / 100);
}
