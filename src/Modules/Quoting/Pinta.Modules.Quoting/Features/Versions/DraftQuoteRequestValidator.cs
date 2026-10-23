using FluentValidation;
using Pinta.Modules.Quoting.Contracts;
using Pinta.Modules.Quoting.Features.Pricing;
using Pinta.Modules.Quoting.Resources;

namespace Pinta.Modules.Quoting.Features.Versions;

/// <summary>Shape of a review draft; the pricing rules are checked by the engine's validator.</summary>
internal sealed class DraftQuoteRequestValidator : AbstractValidator<DraftQuoteRequest>
{
    public DraftQuoteRequestValidator()
    {
        RuleFor(r => r.Occupancy).IsInEnum();
        RuleFor(r => r.Elements).NotNull();
        RuleForEach(r => r.Elements).SetValidator(new QuoteElementRequestValidator());
        RuleForEach(r => r.Extras).ChildRules(extra =>
        {
            extra.RuleFor(x => x.Description).MaximumLength(200);
            extra.RuleFor(x => x.ClientPriceCents).GreaterThanOrEqualTo(0);
            extra.RuleFor(x => x.PainterPayCents).GreaterThanOrEqualTo(0);
            extra.RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.Description) && x.ClientPriceCents is not null && x.PainterPayCents is not null)
                .When(x => string.IsNullOrWhiteSpace(x.CatalogKey))
                .WithName("description")
                .WithMessage(_ => QuotingMessages.Get("extra_needs_price"));
        });
        RuleFor(r => r.ManualSubtotalCents).GreaterThan(0);
        RuleFor(r => r.AdjustmentReason)
            .NotEmpty().WithMessage(_ => QuotingMessages.Get("adjustment_reason_required"))
            .When(r => r.ManualSubtotalCents is not null);
        RuleFor(r => r.AdjustmentReason).MaximumLength(500);
    }
}
