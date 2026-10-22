using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Data;
using Pinta.Modules.Catalog.Domain;
using Pinta.Pricing;

namespace Pinta.Modules.Catalog.Features.PublicCatalog;

/// <summary>An option the client can pick; <see cref="Id"/> is the stable catalog key used in quote requests.</summary>
public sealed record CatalogOption(string Id, string Name);

public sealed record CatalogRoomSize(RoomSize Size, decimal Length, decimal Width);

public sealed record CatalogRoomType(string Id, string Name, string SuggestedPaintId, IReadOnlyList<CatalogRoomSize> Sizes);

public sealed record CatalogResponse(
    IReadOnlyList<CatalogRoomType> RoomTypes,
    IReadOnlyList<CatalogOption> Paints,
    IReadOnlyList<CatalogOption> SurfaceTypes,
    IReadOnlyList<CatalogOption> Occupancies,
    decimal DefaultHeight,
    int ReviewDeadlineBusinessDays,
    int QuoteValidityDays,
    int MinLeadBusinessDays);

/// <summary>
/// GET /api/catalog — what the quote builder offers, in the request language, plus the review deadline and price
/// validity shown on the estimate (C2.8). Never rates or amounts (C2.x).
/// </summary>
internal static class GetCatalogEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/catalog", HandleAsync).WithTags("Catalog").WithName("GetCatalog");

    private static async Task<Ok<CatalogResponse>> HandleAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        var paints = await db.Paints.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.CreatedAt).ThenBy(p => p.Key).ToListAsync(cancellationToken);
        var paintKeys = paints.ToDictionary(p => p.Id, p => p.Key);
        var rooms = await db.RoomTypes.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.CreatedAt).ThenBy(r => r.Key).ToListAsync(cancellationToken);
        var surfaces = await db.SurfaceTypes.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.CreatedAt).ThenBy(s => s.Key).ToListAsync(cancellationToken);
        var occupancies = await db.Modifiers.AsNoTracking()
            .Where(m => m.IsActive && m.Group == Modifier.OccupancyGroup)
            .OrderBy(m => m.Factor)
            .ToListAsync(cancellationToken);
        var settings = await db.Settings.AsNoTracking()
            .Select(s => new { s.DefaultHeight, s.ReviewDeadlineBusinessDays, s.QuoteValidityDays, s.MinLeadBusinessDays })
            .SingleAsync(cancellationToken);

        return TypedResults.Ok(new CatalogResponse(
            rooms.Select(r => new CatalogRoomType(
                r.Key,
                r.Name.Current,
                paintKeys.GetValueOrDefault(r.SuggestedPaintId, paints[0].Key),
                [
                    new(RoomSize.S, r.Sizes.S.Length, r.Sizes.S.Width),
                    new(RoomSize.M, r.Sizes.M.Length, r.Sizes.M.Width),
                    new(RoomSize.L, r.Sizes.L.Length, r.Sizes.L.Width),
                ])).ToArray(),
            paints.Select(p => new CatalogOption(p.Key, p.Name.Current)).ToArray(),
            surfaces.Select(s => new CatalogOption(s.Key, s.Name.Current)).ToArray(),
            occupancies.Select(m => new CatalogOption(m.Key, m.Name.Current)).ToArray(),
            settings.DefaultHeight,
            settings.ReviewDeadlineBusinessDays,
            settings.QuoteValidityDays,
            settings.MinLeadBusinessDays));
    }
}
