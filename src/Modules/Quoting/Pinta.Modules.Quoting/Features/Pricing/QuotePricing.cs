using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Pinta.Modules.Catalog.Contracts;
using Pinta.Modules.Quoting.Contracts;
using Pinta.Modules.Quoting.Resources;
using Pinta.Pricing;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Quoting.Features.Pricing;

/// <summary>Shape of the request elements; the pricing rules themselves are checked by <see cref="QuoteValidator"/>.</summary>
internal sealed class QuoteElementRequestValidator : AbstractValidator<QuoteElementRequest>
{
    public QuoteElementRequestValidator()
    {
        RuleFor(e => e.Kind).IsInEnum();
        RuleFor(e => e.Name).MaximumLength(80);
        RuleFor(e => e.PaintId).NotEmpty();
        When(e => e.Kind == ElementKind.Room, () =>
        {
            RuleFor(e => e.RoomTypeId).NotEmpty();
            RuleFor(e => e.Walls).NotNull().Must(w => w!.Length == 4).WithMessage(_ => QuotingMessages.Get("walls_four"));
        });
        When(e => e.Kind == ElementKind.Surface, () =>
        {
            RuleFor(e => e.SurfaceTypeId).NotEmpty();
            RuleFor(e => e.Length).NotNull();
            RuleFor(e => e.Width).NotNull();
        });
    }
}

/// <summary>Turns API requests into <see cref="QuoteInput"/>, prices them with the current snapshot and reports 422s.</summary>
internal sealed class QuotePricing(IPriceSnapshotProvider snapshots)
{
    public async Task<(QuoteBreakdown? Breakdown, ProblemHttpResult? Problem)> PriceAsync(
        Occupancy occupancy, IReadOnlyList<QuoteElementRequest> elements, IReadOnlyList<ExtraLine> extras,
        CancellationToken cancellationToken)
    {
        var snapshot = await snapshots.GetCurrentAsync(cancellationToken);
        var input = new QuoteInput([.. elements.Select(ToElement)], OccupancyKey(occupancy), extras);
        var errors = QuoteValidator.Validate(snapshot, input);
        if (errors.Count > 0)
        {
            return (null, ApiProblem.Validation(errors
                .GroupBy(e => e.Path)
                .ToDictionary(g => g.Key, g => g.Select(e => QuotingMessages.Get(e.Code)).ToArray())));
        }

        return (PricingEngine.Calculate(snapshot, input), null);
    }

    private static string OccupancyKey(Occupancy occupancy) => occupancy.ToString().ToLowerInvariant();

    private static QuoteElement ToElement(QuoteElementRequest e) => e.Kind == ElementKind.Room
        ? new Room(e.RoomTypeId!, e.Size, e.Length, e.Width, e.Height, new Walls(e.Walls![0], e.Walls[1], e.Walls[2], e.Walls[3]),
            e.Ceiling, e.PaintId, e.Damage)
        : new LooseSurface(e.SurfaceTypeId!, e.Length!.Value, e.Width!.Value, e.PaintId, e.Damage);
}

public sealed record ElementAreaDto(int Index, decimal? WallsAreaM2, decimal? CeilingAreaM2, decimal AreaM2)
{
    internal static ElementAreaDto From(ElementArea a) => new(a.Index, a.WallsAreaM2, a.CeilingAreaM2, a.AreaM2);
}
