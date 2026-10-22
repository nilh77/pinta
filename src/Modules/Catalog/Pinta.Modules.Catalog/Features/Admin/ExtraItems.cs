using FluentValidation;
using Pinta.Modules.Catalog.Domain;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Text;

namespace Pinta.Modules.Catalog.Features.Admin;

public sealed record ExtraItemDto(
    Guid Id, string Key, LocalizedTextDto Name, long ClientPriceCents, long PainterPayCents, bool IsActive, uint Version,
    DateTimeOffset UpdatedAt);

public sealed record ExtraItemRequest(
    string? Key, LocalizedTextDto Name, long ClientPriceCents, long PainterPayCents, bool IsActive, uint? Version)
    : ICatalogItemRequest;

internal sealed class ExtraItemRequestValidator : AbstractValidator<ExtraItemRequest>
{
    public ExtraItemRequestValidator()
    {
        RuleFor(r => r.Name).Name();
        RuleFor(r => r.ClientPriceCents).GreaterThanOrEqualTo(0);
        RuleFor(r => r.PainterPayCents).GreaterThanOrEqualTo(0);
    }
}

/// <summary>A1.5 — catalog of extra items, before VAT.</summary>
internal static class ExtraItems
{
    public static readonly CatalogCrud<ExtraItem, ExtraItemRequest, ExtraItemDto> Crud = new(
        "extras",
        "ExtraItem",
        "ExtraItems",
        x => new(x.Id, x.Key, LocalizedTextDto.From(x.Name), Money.ToCents(x.ClientPrice), Money.ToCents(x.PainterPay),
            x.IsActive, x.Version, x.UpdatedAt),
        r => new ExtraItem { Key = r.Key!, Name = r.Name.ToText() },
        (r, x) =>
        {
            x.Name = r.Name.ToText();
            x.ClientPrice = Money.FromCents(r.ClientPriceCents);
            x.PainterPay = Money.FromCents(r.PainterPayCents);
            x.IsActive = r.IsActive;
        });
}
