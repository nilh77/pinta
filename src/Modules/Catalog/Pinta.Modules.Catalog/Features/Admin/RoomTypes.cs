using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Domain;
using Pinta.Modules.Catalog.Resources;
using Pinta.Pricing;
using Pinta.SharedKernel.Text;

namespace Pinta.Modules.Catalog.Features.Admin;

public sealed record RoomMeasuresDto(decimal Length, decimal Width);

public sealed record RoomSizesDto(RoomMeasuresDto S, RoomMeasuresDto M, RoomMeasuresDto L);

public sealed record RoomTypeDto(
    Guid Id, string Key, LocalizedTextDto Name, RoomSizesDto Sizes, Guid SuggestedPaintId, bool IsActive, uint Version,
    DateTimeOffset UpdatedAt);

public sealed record RoomTypeRequest(
    string? Key, LocalizedTextDto Name, RoomSizesDto Sizes, Guid SuggestedPaintId, bool IsActive, uint? Version)
    : ICatalogItemRequest;

internal sealed class RoomTypeRequestValidator : AbstractValidator<RoomTypeRequest>
{
    public RoomTypeRequestValidator()
    {
        RuleFor(r => r.Name).Name();
        RuleFor(r => r.Sizes).NotNull().ChildRules(sizes =>
        {
            sizes.RuleFor(s => s.S).NotNull().SetValidator(new MeasuresValidator());
            sizes.RuleFor(s => s.M).NotNull().SetValidator(new MeasuresValidator());
            sizes.RuleFor(s => s.L).NotNull().SetValidator(new MeasuresValidator());
        });
        RuleFor(r => r.SuggestedPaintId).NotEmpty();
    }

    private sealed class MeasuresValidator : AbstractValidator<RoomMeasuresDto>
    {
        public MeasuresValidator()
        {
            // Same bounds as an exact room (docs/pricing-engine.md, "Validaciones").
            RuleFor(m => m.Length).InclusiveBetween(QuoteValidator.MinSide, QuoteValidator.MaxRoomSide).TwoDecimals();
            RuleFor(m => m.Width).InclusiveBetween(QuoteValidator.MinSide, QuoteValidator.MaxRoomSide).TwoDecimals();
        }
    }
}

/// <summary>A1.3 — room types: S/M/L sizes and suggested paint.</summary>
internal static class RoomTypes
{
    public static readonly CatalogCrud<RoomType, RoomTypeRequest, RoomTypeDto> Crud = new(
        "room-types",
        "RoomType",
        "RoomTypes",
        r => new(r.Id, r.Key, LocalizedTextDto.From(r.Name), ToDto(r.Sizes), r.SuggestedPaintId, r.IsActive, r.Version, r.UpdatedAt),
        r => new RoomType { Key = r.Key!, Name = r.Name.ToText(), Sizes = ToSizes(r.Sizes) },
        (request, room) =>
        {
            room.Name = request.Name.ToText();
            room.Sizes = ToSizes(request.Sizes);
            room.SuggestedPaintId = request.SuggestedPaintId;
            room.IsActive = request.IsActive;
        },
        async (db, request, _, cancellationToken) =>
            await db.Paints.AnyAsync(p => p.Id == request.SuggestedPaintId && p.IsActive, cancellationToken)
                ? []
                : new() { ["suggestedPaintId"] = [CatalogMessages.Get("suggested_paint_inactive")] });

    private static RoomSizesDto ToDto(RoomSizes sizes) =>
        new(new(sizes.S.Length, sizes.S.Width), new(sizes.M.Length, sizes.M.Width), new(sizes.L.Length, sizes.L.Width));

    private static RoomSizes ToSizes(RoomSizesDto sizes) => new()
    {
        S = new RoomMeasures { Length = sizes.S.Length, Width = sizes.S.Width },
        M = new RoomMeasures { Length = sizes.M.Length, Width = sizes.M.Width },
        L = new RoomMeasures { Length = sizes.L.Length, Width = sizes.L.Width },
    };
}
