using FluentValidation;
using Pinta.Modules.Catalog.Resources;
using Pinta.SharedKernel.Text;

namespace Pinta.Modules.Catalog.Features.Admin;

/// <summary>Validation rules shared by the catalog editors (A1.1): Portuguese name required, amounts ≥ 0, factors 0.5–3.</summary>
internal static class CatalogRules
{
    public const string KeyPattern = "^[a-z][a-z0-9_]{1,39}$";
    public const decimal MinFactor = 0.5m;
    public const decimal MaxFactor = 3m;

    public static IRuleBuilderOptions<T, decimal> TwoDecimals<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.Must(value => value == Math.Round(value, 2)).WithMessage(_ => CatalogMessages.Get("two_decimals"));

    public static IRuleBuilderOptions<T, decimal> Factor<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.InclusiveBetween(MinFactor, MaxFactor).TwoDecimals();

    public static IRuleBuilderOptions<T, decimal> Percentage<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.InclusiveBetween(0m, 100m).TwoDecimals();

    public static void Name<T>(this IRuleBuilder<T, LocalizedTextDto> rule) =>
        rule.NotNull().ChildRules(name =>
        {
            name.RuleFor(n => n.Pt).NotEmpty().MaximumLength(120);
            name.RuleFor(n => n.Es).MaximumLength(120);
            name.RuleFor(n => n.En).MaximumLength(120);
        });
}
