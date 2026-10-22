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

/// <summary>A priced request: the snapshot used, the engine's input and result, or the validation errors.</summary>
internal sealed record PricedQuote(PriceSnapshot Snapshot, QuoteInput Input, QuoteBreakdown? Breakdown, Dictionary<string, string[]>? Errors);

/// <summary>Turns API requests into <see cref="QuoteInput"/>, prices them with the current snapshot and reports 422s.</summary>
internal sealed class QuotePricing(IPriceSnapshotProvider snapshots)
{
    public async Task<(QuoteBreakdown? Breakdown, ProblemHttpResult? Problem)> PriceAsync(
        Occupancy occupancy, IReadOnlyList<QuoteElementRequest> elements, IReadOnlyList<ExtraLine> extras,
        CancellationToken cancellationToken)
    {
        var priced = await PriceWithSnapshotAsync(occupancy, elements, extras, cancellationToken);
        return priced.Errors is null ? (priced.Breakdown, null) : (null, ApiProblem.Validation(priced.Errors));
    }

    public async Task<PricedQuote> PriceWithSnapshotAsync(
        Occupancy occupancy, IReadOnlyList<QuoteElementRequest> elements, IReadOnlyList<ExtraLine> extras,
        CancellationToken cancellationToken) =>
        PriceWith(await snapshots.GetCurrentAsync(cancellationToken), occupancy, elements, extras);

    /// <summary>Prices with a given snapshot: a draft keeps the one taken when it started.</summary>
    public static PricedQuote PriceWith(
        PriceSnapshot snapshot, Occupancy occupancy, IReadOnlyList<QuoteElementRequest> elements, IReadOnlyList<ExtraLine> extras,
        decimal? manualSubtotal = null)
    {
        var input = new QuoteInput([.. elements.Select(ToElement)], OccupancyKey(occupancy), extras) { ManualSubtotal = manualSubtotal };
        var errors = QuoteValidator.Validate(snapshot, input);
        if (errors.Count > 0)
        {
            return new PricedQuote(snapshot, input, null, errors
                .GroupBy(e => e.Path == "manualSubtotal" ? "manualSubtotalCents" : e.Path)
                .ToDictionary(g => g.Key, g => g.Select(e => QuotingMessages.Get(e.Code)).ToArray()));
        }

        return new PricedQuote(snapshot, input, PricingEngine.Calculate(snapshot, input), null);
    }

    public static string OccupancyKey(Occupancy occupancy) => occupancy.ToString().ToLowerInvariant();

    public static Occupancy ParseOccupancy(string key) => Enum.Parse<Occupancy>(key, ignoreCase: true);

    public static QuoteElement ToElement(QuoteElementRequest e) => e.Kind == ElementKind.Room
        ? new Room(e.RoomTypeId!, e.Size, e.Length, e.Width, e.Height, new Walls(e.Walls![0], e.Walls[1], e.Walls[2], e.Walls[3]),
            e.Ceiling, e.PaintId, e.Damage)
        : new LooseSurface(e.SurfaceTypeId!, e.Length!.Value, e.Width!.Value, e.PaintId, e.Damage);
}
