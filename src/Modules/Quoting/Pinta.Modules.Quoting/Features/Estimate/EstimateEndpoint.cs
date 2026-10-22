using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Pinta.Modules.Quoting.Contracts;
using Pinta.Modules.Quoting.Features.Pricing;
using Pinta.Modules.Quoting.Features.Versions;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Quoting.Features.Estimate;

public sealed record EstimateRequest(Occupancy Occupancy, IReadOnlyList<QuoteElementRequest> Elements);

internal sealed class EstimateRequestValidator : AbstractValidator<EstimateRequest>
{
    public EstimateRequestValidator()
    {
        RuleFor(r => r.Occupancy).IsInEnum();
        RuleFor(r => r.Elements).NotNull();
        RuleForEach(r => r.Elements).SetValidator(new QuoteElementRequestValidator());
    }
}

/// <summary>Only what the client may see: areas, range with VAT and flags. Never rates, material, painter pay or margin.</summary>
public sealed record EstimateResponse(
    decimal AreaM2,
    IReadOnlyList<ElementAreaDto> Elements,
    long? RangeMinCents,
    long? RangeMaxCents,
    bool MinimumApplied,
    bool OverMax,
    bool Premium,
    bool RangeWidened);

/// <summary>POST /api/quotes/estimate — public estimate before the client gives any data (C2.8). 60 per minute per IP.</summary>
internal static class EstimateEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/quotes/estimate", HandleAsync)
            .WithTags("Quotes")
            .WithValidation<EstimateRequest>()
            .RequireRateLimiting(RateLimitPolicies.Estimate)
            .WithName("EstimateQuote")
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<Results<Ok<EstimateResponse>, ProblemHttpResult>> HandleAsync(
        EstimateRequest request, QuotePricing pricing, CancellationToken cancellationToken)
    {
        var (breakdown, problem) = await pricing.PriceAsync(request.Occupancy, request.Elements, [], cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        return TypedResults.Ok(new EstimateResponse(
            breakdown!.AreaM2,
            breakdown.Elements.Select(BreakdownMapper.Area).ToArray(),
            breakdown.Range is null ? null : Money.ToCents(breakdown.Range.Min),
            breakdown.Range is null ? null : Money.ToCents(breakdown.Range.Max),
            breakdown.MinimumApplied,
            breakdown.OverMax,
            breakdown.Premium,
            breakdown.RangeWidened));
    }
}
