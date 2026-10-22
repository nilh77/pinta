using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Pinta.Modules.Quoting.Contracts;
using Pinta.Modules.Quoting.Features.Pricing;
using Pinta.Pricing;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Quoting.Features.Simulate;

public sealed record SimulateExtraRequest(string Description, long ClientPriceCents, long PainterPayCents);

public sealed record SimulateRequest(
    Occupancy Occupancy, IReadOnlyList<QuoteElementRequest> Elements, IReadOnlyList<SimulateExtraRequest>? Extras);

internal sealed class SimulateRequestValidator : AbstractValidator<SimulateRequest>
{
    public SimulateRequestValidator()
    {
        RuleFor(r => r.Occupancy).IsInEnum();
        RuleFor(r => r.Elements).NotNull();
        RuleForEach(r => r.Elements).SetValidator(new QuoteElementRequestValidator());
        RuleForEach(r => r.Extras).ChildRules(extra =>
        {
            extra.RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
            extra.RuleFor(x => x.ClientPriceCents).GreaterThanOrEqualTo(0);
            extra.RuleFor(x => x.PainterPayCents).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed record PricedLineDto(
    int Element, string Surface, decimal AreaM2, decimal Litres, long MaterialCents, long LabourCents, long PainterCents);

public sealed record ExtraLineDto(string Description, long ClientPriceCents, long PainterPayCents);

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

/// <summary>POST /api/admin/quotes/simulate — prices sample elements with the current configuration. Saves nothing (A1.8).</summary>
internal static class SimulateEndpoint
{
    public static void Map(RouteGroupBuilder admin) =>
        admin.MapPost("/quotes/simulate", HandleAsync).WithValidation<SimulateRequest>().WithName("SimulateQuote");

    private static async Task<Results<Ok<QuoteBreakdownDto>, ProblemHttpResult>> HandleAsync(
        SimulateRequest request, QuotePricing pricing, CancellationToken cancellationToken)
    {
        var extras = request.Extras ?? [];
        var (breakdown, problem) = await pricing.PriceAsync(
            request.Occupancy,
            request.Elements,
            [.. extras.Select(x => new ExtraLine(Money.FromCents(x.ClientPriceCents), Money.FromCents(x.PainterPayCents)))],
            cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        var b = breakdown!;
        return TypedResults.Ok(new QuoteBreakdownDto(
            b.Lines.Select(l => new PricedLineDto(l.ElementIndex, l.SurfaceType, l.AreaM2, l.Litres,
                Money.ToCents(l.MaterialCost), Money.ToCents(l.LabourPrice), Money.ToCents(l.PainterPay))).ToArray(),
            extras.Select(x => new ExtraLineDto(x.Description, x.ClientPriceCents, x.PainterPayCents)).ToArray(),
            b.Elements.Select(ElementAreaDto.From).ToArray(),
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
            b.RangeWidened));
    }
}
