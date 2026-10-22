using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Contracts;
using Pinta.Modules.Catalog.Data;
using Pinta.Modules.Catalog.Domain;
using Pinta.Pricing;

namespace Pinta.Modules.Catalog.Features.Snapshot;

internal sealed class PriceSnapshotProvider(CatalogDbContext db) : IPriceSnapshotProvider
{
    public async Task<PriceSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var paints = await db.Paints.AsNoTracking().Where(p => p.IsActive).ToListAsync(cancellationToken);
        var surfaces = await db.SurfaceTypes.AsNoTracking().Where(s => s.IsActive).ToListAsync(cancellationToken);
        var rooms = await db.RoomTypes.AsNoTracking().Where(r => r.IsActive).ToListAsync(cancellationToken);
        var occupancies = await db.Modifiers.AsNoTracking()
            .Where(m => m.IsActive && m.Group == Modifier.OccupancyGroup)
            .ToListAsync(cancellationToken);
        var settings = await db.Settings.AsNoTracking().SingleAsync(cancellationToken);

        return new PriceSnapshot(
            paints.ToDictionary(p => p.Key, p => new PaintRate(p.PricePerLitre, p.CoverageM2PerLitre, p.Coats)),
            surfaces.ToDictionary(s => s.Key, s => new SurfaceRate(s.ClientRatePerM2, s.PainterRatePerM2, s.DifficultyFactor)),
            occupancies.ToDictionary(m => m.Key, m => m.Factor),
            rooms.ToDictionary(r => r.Key, r => new RoomSizeTable(Dimensions(r.Sizes.S), Dimensions(r.Sizes.M), Dimensions(r.Sizes.L))),
            settings.ToRules());
    }

    private static RoomDimensions Dimensions(RoomMeasures measures) => new(measures.Length, measures.Width);
}
