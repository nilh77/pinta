using FluentValidation;
using Pinta.Modules.Catalog.Domain;
using Pinta.Modules.Catalog.Resources;
using Pinta.SharedKernel.Text;

namespace Pinta.Modules.Catalog.Features.Admin;

public sealed record ModifierDto(
    Guid Id, string Group, string Key, LocalizedTextDto Name, decimal Factor, bool IsActive, uint Version,
    DateTimeOffset UpdatedAt);

public sealed record ModifierRequest(string? Key, LocalizedTextDto Name, decimal Factor, bool IsActive, uint? Version)
    : ICatalogItemRequest;

internal sealed class ModifierRequestValidator : AbstractValidator<ModifierRequest>
{
    public ModifierRequestValidator()
    {
        RuleFor(r => r.Name).Name();
        RuleFor(r => r.Factor).Factor();
    }
}

/// <summary>A1.4 — occupancy modifiers. Keys are the API's <c>Occupancy</c> values (docs/api.md).</summary>
internal static class Modifiers
{
    public static readonly string[] OccupancyKeys = ["empty", "furnished", "inhabited"];

    public static readonly CatalogCrud<Modifier, ModifierRequest, ModifierDto> Crud = new(
        "modifiers",
        "Modifier",
        "Modifiers",
        m => new(m.Id, m.Group, m.Key, LocalizedTextDto.From(m.Name), m.Factor, m.IsActive, m.Version, m.UpdatedAt),
        r => new Modifier { Key = r.Key!, Name = r.Name.ToText() },
        (r, m) =>
        {
            m.Name = r.Name.ToText();
            m.Factor = r.Factor;
            m.IsActive = r.IsActive;
        },
        (_, request, existing, _) => Task.FromResult(
            existing is null && request.Key is not null && !OccupancyKeys.Contains(request.Key)
                ? new Dictionary<string, string[]> { ["key"] = [CatalogMessages.Get("occupancy_key")] }
                : []));
}
