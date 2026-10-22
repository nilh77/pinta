using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Domain;
using Pinta.Modules.Catalog.Resources;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Text;

namespace Pinta.Modules.Catalog.Features.Admin;

public sealed record PaintDto(
    Guid Id, string Key, LocalizedTextDto Name, long PricePerLitreCents, decimal CoverageM2PerLitre, int Coats,
    bool IsActive, uint Version, DateTimeOffset UpdatedAt);

public sealed record PaintRequest(
    string? Key, LocalizedTextDto Name, long PricePerLitreCents, decimal CoverageM2PerLitre, int Coats, bool IsActive,
    uint? Version) : ICatalogItemRequest;

internal sealed class PaintRequestValidator : AbstractValidator<PaintRequest>
{
    public PaintRequestValidator()
    {
        RuleFor(r => r.Name).Name();
        RuleFor(r => r.PricePerLitreCents).GreaterThanOrEqualTo(0);
        RuleFor(r => r.CoverageM2PerLitre).GreaterThan(0).LessThanOrEqualTo(100).TwoDecimals();
        RuleFor(r => r.Coats).InclusiveBetween(1, 5);
    }
}

/// <summary>A1.1 — paints: price per litre, coverage and coats.</summary>
internal static class Paints
{
    public static readonly CatalogCrud<Paint, PaintRequest, PaintDto> Crud = new(
        "paints",
        "Paint",
        "Paints",
        p => new(p.Id, p.Key, LocalizedTextDto.From(p.Name), Money.ToCents(p.PricePerLitre), p.CoverageM2PerLitre,
            p.Coats, p.IsActive, p.Version, p.UpdatedAt),
        r => new Paint { Key = r.Key!, Name = r.Name.ToText() },
        (r, p) =>
        {
            p.Name = r.Name.ToText();
            p.PricePerLitre = Money.FromCents(r.PricePerLitreCents);
            p.CoverageM2PerLitre = r.CoverageM2PerLitre;
            p.Coats = r.Coats;
            p.IsActive = r.IsActive;
        },
        async (db, request, paint, cancellationToken) =>
        {
            // A paint suggested for a room type cannot be deactivated until another suggestion is chosen (A1.1–A1.5).
            if (paint is null || request.IsActive || !paint.IsActive)
            {
                return [];
            }

            var rooms = await db.RoomTypes.Where(r => r.SuggestedPaintId == paint.Id).ToListAsync(cancellationToken);
            return rooms.Count == 0
                ? []
                : new() { ["isActive"] = [CatalogMessages.Get("suggested_paint_in_use", string.Join(", ", rooms.Select(r => r.Name.Current)))] };
        });
}
