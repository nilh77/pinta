using FluentValidation;
using Pinta.Modules.Catalog.Domain;
using Pinta.Modules.Catalog.Resources;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Text;

namespace Pinta.Modules.Catalog.Features.Admin;

public sealed record SurfaceTypeDto(
    Guid Id, string Key, LocalizedTextDto Name, long ClientRateCents, long PainterRateCents, decimal DifficultyFactor,
    bool IsActive, uint Version, DateTimeOffset UpdatedAt);

public sealed record SurfaceTypeRequest(
    string? Key, LocalizedTextDto Name, long ClientRateCents, long PainterRateCents, decimal DifficultyFactor,
    bool IsActive, uint? Version) : ICatalogItemRequest;

internal sealed class SurfaceTypeRequestValidator : AbstractValidator<SurfaceTypeRequest>
{
    public SurfaceTypeRequestValidator()
    {
        RuleFor(r => r.Name).Name();
        RuleFor(r => r.ClientRateCents).GreaterThanOrEqualTo(0);
        RuleFor(r => r.PainterRateCents).GreaterThanOrEqualTo(0);
        RuleFor(r => r.DifficultyFactor).Factor();
    }
}

/// <summary>A1.2 — surface types: client rate, painter pay and difficulty factor per m².</summary>
internal static class SurfaceTypes
{
    public static readonly CatalogCrud<SurfaceType, SurfaceTypeRequest, SurfaceTypeDto> Crud = new(
        "surfaces",
        "SurfaceType",
        "SurfaceTypes",
        s => new(s.Id, s.Key, LocalizedTextDto.From(s.Name), Money.ToCents(s.ClientRatePerM2),
            Money.ToCents(s.PainterRatePerM2), s.DifficultyFactor, s.IsActive, s.Version, s.UpdatedAt),
        r => new SurfaceType { Key = r.Key!, Name = r.Name.ToText() },
        (r, s) =>
        {
            s.Name = r.Name.ToText();
            s.ClientRatePerM2 = Money.FromCents(r.ClientRateCents);
            s.PainterRatePerM2 = Money.FromCents(r.PainterRateCents);
            s.DifficultyFactor = r.DifficultyFactor;
            s.IsActive = r.IsActive;
        },
        (_, request, surface, _) => Task.FromResult(
            surface is not null && !request.IsActive && SurfaceType.RoomSurfaceKeys.Contains(surface.Key)
                ? new Dictionary<string, string[]> { ["isActive"] = [CatalogMessages.Get("room_surface_required")] }
                : []));
}
