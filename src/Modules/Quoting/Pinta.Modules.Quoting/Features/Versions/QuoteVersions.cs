using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Quoting.Contracts;
using Pinta.Modules.Quoting.Data;
using Pinta.Modules.Quoting.Domain;
using Pinta.Modules.Quoting.Features.Estimate;
using Pinta.Modules.Quoting.Features.Pricing;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;
using StoredElement = Pinta.Modules.Quoting.Domain.QuoteElement;

namespace Pinta.Modules.Quoting.Features.Versions;

internal sealed class QuoteVersions(QuotingDbContext db, QuotePricing pricing, TimeProvider clock) : IQuoteVersions
{
    public async Task<(QuoteVersionSummary? Version, QuoteErrors? Errors)> CreateEstimateAsync(
        Guid jobId, Guid createdBy, Occupancy occupancy, IReadOnlyList<QuoteElementRequest> elements, CancellationToken cancellationToken = default)
    {
        // Same checks as POST /api/quotes/estimate: request shape first, then the pricing rules.
        var shape = await new EstimateRequestValidator().ValidateAsync(new EstimateRequest(occupancy, elements), cancellationToken);
        if (!shape.IsValid)
        {
            return (null, new QuoteErrors(ValidationFilter<EstimateRequest>.ToErrors(shape)));
        }

        var priced = await pricing.PriceWithSnapshotAsync(occupancy, elements, [], cancellationToken);
        if (priced.Errors is not null)
        {
            return (null, new QuoteErrors(priced.Errors));
        }

        var b = priced.Breakdown!;
        var version = new QuoteVersion
        {
            JobId = jobId,
            Number = 1,
            Origin = QuoteOrigin.Estimate,
            PriceSnapshot = priced.Snapshot,
            Occupancy = priced.Input.Occupancy,
            AreaM2 = b.AreaM2,
            MaterialCost = b.MaterialCost,
            LabourPrice = b.LabourPrice,
            Subtotal = b.Subtotal,
            Vat = b.Vat,
            Total = b.Total,
            PainterPay = b.PainterPay,
            Margin = b.Margin,
            RangeMin = b.Range?.Min,
            RangeMax = b.Range?.Max,
            MinimumApplied = b.MinimumApplied,
            OverMax = b.OverMax,
            Premium = b.Premium,
            CreatedBy = createdBy,
            CreatedAt = clock.GetUtcNow(),
        };
        for (var i = 0; i < elements.Count; i++)
        {
            var e = elements[i];
            version.Elements.Add(new StoredElement
            {
                QuoteVersionId = version.Id,
                Position = i,
                ClientKey = e.ClientKey,
                Name = e.Name?.Trim(),
                Kind = e.Kind == ElementKind.Room ? "room" : "surface",
                RoomType = e.Kind == ElementKind.Room ? e.RoomTypeId : null,
                SurfaceType = e.Kind == ElementKind.Surface ? e.SurfaceTypeId : null,
                Size = e.Size,
                Length = e.Length,
                Width = e.Width,
                Height = e.Height,
                WallA = e.Walls?[0] ?? false,
                WallB = e.Walls?[1] ?? false,
                WallC = e.Walls?[2] ?? false,
                WallD = e.Walls?[3] ?? false,
                Ceiling = e.Kind == ElementKind.Room && e.Ceiling,
                Paint = e.PaintId,
                HasDamage = e.Damage,
                AreaM2 = b.Elements[i].AreaM2,
            });
        }

        db.Versions.Add(version);
        await db.SaveChangesAsync(cancellationToken);
        return (ToSummary(version), null);
    }

    public async Task<QuoteVersionSummary?> GetAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var version = await db.Versions.AsNoTracking().Include(v => v.Elements).SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken);
        return version is null ? null : ToSummary(version);
    }

    private static QuoteVersionSummary ToSummary(QuoteVersion v) => new(
        v.Id,
        v.Number,
        v.AreaM2,
        Money.ToCents(v.Total),
        v.RangeMin is null ? null : Money.ToCents(v.RangeMin.Value),
        v.RangeMax is null ? null : Money.ToCents(v.RangeMax.Value),
        v.MinimumApplied,
        v.OverMax,
        v.Premium,
        v.Elements.Where(e => e.ClientKey is not null).GroupBy(e => e.ClientKey!).ToDictionary(g => g.Key, g => g.First().Id));
}
