using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Pinta.Modules.Quoting.Contracts;
using Pinta.Modules.Quoting.Features.Pricing;
using Pinta.Pricing;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;
using Pinta.Modules.Quoting.Features.Versions;

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

        return TypedResults.Ok(BreakdownMapper.ToDto(
            breakdown!, [.. extras.Select(x => new ExtraLineDto(x.Description, x.ClientPriceCents, x.PainterPayCents))]));
    }
}
