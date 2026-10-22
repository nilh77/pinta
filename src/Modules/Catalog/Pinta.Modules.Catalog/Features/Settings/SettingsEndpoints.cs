using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Data;
using Pinta.Modules.Catalog.Domain;
using Pinta.Modules.Catalog.Features.Admin;
using Pinta.Modules.Catalog.Resources;
using Pinta.Pricing;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Data;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Catalog.Features.Settings;

public sealed record SettingsDto(
    decimal WastePct,
    decimal VatPct,
    long MinJobAmountCents,
    long MaxJobAmountCents,
    decimal PremiumAreaM2,
    decimal RangeLowPct,
    decimal RangeHighPct,
    decimal RangeSizeWideningPct,
    decimal RangeDamageWideningPct,
    decimal DepositPct,
    decimal MinMarginPct,
    decimal DefaultHeight,
    int QuoteValidityDays,
    long SiteVisitPriceCents,
    bool SiteVisitDeductible,
    int ReviewDeadlineBusinessDays,
    int FullRefundMinDays,
    decimal LateRefundPct,
    int MaxPhotosPerElement,
    int MaxVideosPerElement,
    int MaxImageSizeMb,
    int MaxVideoSizeMb,
    bool ClientPhoneVisibleToPainter,
    bool PainterPhoneVisibleToClient,
    uint Version,
    DateTimeOffset UpdatedAt);

internal sealed class SettingsDtoValidator : AbstractValidator<SettingsDto>
{
    public SettingsDtoValidator()
    {
        RuleFor(s => s.WastePct).Percentage();
        RuleFor(s => s.VatPct).Percentage();
        RuleFor(s => s.MinJobAmountCents).GreaterThanOrEqualTo(0);
        RuleFor(s => s.MaxJobAmountCents).GreaterThan(s => s.MinJobAmountCents)
            .WithMessage(_ => CatalogMessages.Get("min_below_max"));
        RuleFor(s => s.PremiumAreaM2).GreaterThan(0).TwoDecimals();
        RuleFor(s => s.RangeLowPct).Percentage();
        RuleFor(s => s.RangeHighPct).Percentage();
        RuleFor(s => s.RangeSizeWideningPct).Percentage();
        RuleFor(s => s.RangeDamageWideningPct).Percentage();
        RuleFor(s => s.DepositPct).Percentage();
        RuleFor(s => s.MinMarginPct).Percentage();
        RuleFor(s => s.DefaultHeight).InclusiveBetween(QuoteValidator.MinHeight, QuoteValidator.MaxHeight).TwoDecimals();
        RuleFor(s => s.QuoteValidityDays).InclusiveBetween(1, 365);
        RuleFor(s => s.SiteVisitPriceCents).GreaterThanOrEqualTo(0);
        RuleFor(s => s.ReviewDeadlineBusinessDays).InclusiveBetween(1, 30);
        RuleFor(s => s.FullRefundMinDays).InclusiveBetween(0, 365);
        RuleFor(s => s.LateRefundPct).Percentage();
        RuleFor(s => s.MaxPhotosPerElement).InclusiveBetween(0, 50);
        RuleFor(s => s.MaxVideosPerElement).InclusiveBetween(0, 10);
        RuleFor(s => s.MaxImageSizeMb).InclusiveBetween(1, 50);
        RuleFor(s => s.MaxVideoSizeMb).InclusiveBetween(1, 500);
    }
}

/// <summary>GET / PUT /api/admin/settings — global configuration (A1.6). Applies to quotes calculated from now on.</summary>
internal static class SettingsEndpoints
{
    public static void Map(RouteGroupBuilder admin)
    {
        admin.MapGet("/settings", GetAsync).WithName("GetSettings");
        admin.MapPut("/settings", UpdateAsync).WithValidation<SettingsDto>().WithName("UpdateSettings")
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<Ok<SettingsDto>> GetAsync(CatalogDbContext db, CancellationToken cancellationToken) =>
        TypedResults.Ok(ToDto(await db.Settings.AsNoTracking().SingleAsync(cancellationToken)));

    private static async Task<Results<Ok<SettingsDto>, ProblemHttpResult>> UpdateAsync(
        SettingsDto request, CatalogDbContext db, TimeProvider clock, HttpContext http, CancellationToken cancellationToken)
    {
        var settings = await db.Settings.SingleAsync(cancellationToken);
        settings.WastePct = request.WastePct;
        settings.VatPct = request.VatPct;
        settings.MinJobAmount = Money.FromCents(request.MinJobAmountCents);
        settings.MaxJobAmount = Money.FromCents(request.MaxJobAmountCents);
        settings.PremiumAreaM2 = request.PremiumAreaM2;
        settings.RangeLowPct = request.RangeLowPct;
        settings.RangeHighPct = request.RangeHighPct;
        settings.RangeSizeWideningPct = request.RangeSizeWideningPct;
        settings.RangeDamageWideningPct = request.RangeDamageWideningPct;
        settings.DepositPct = request.DepositPct;
        settings.MinMarginPct = request.MinMarginPct;
        settings.DefaultHeight = request.DefaultHeight;
        settings.QuoteValidityDays = request.QuoteValidityDays;
        settings.SiteVisitPrice = Money.FromCents(request.SiteVisitPriceCents);
        settings.SiteVisitDeductible = request.SiteVisitDeductible;
        settings.ReviewDeadlineBusinessDays = request.ReviewDeadlineBusinessDays;
        settings.FullRefundMinDays = request.FullRefundMinDays;
        settings.LateRefundPct = request.LateRefundPct;
        settings.MaxPhotosPerElement = request.MaxPhotosPerElement;
        settings.MaxVideosPerElement = request.MaxVideosPerElement;
        settings.MaxImageSizeMb = request.MaxImageSizeMb;
        settings.MaxVideoSizeMb = request.MaxVideoSizeMb;
        settings.ClientPhoneVisibleToPainter = request.ClientPhoneVisibleToPainter;
        settings.PainterPhoneVisibleToClient = request.PainterPhoneVisibleToClient;
        settings.Stamp(clock.GetUtcNow(), http.User.GetUserId());

        if (!await db.TrySaveVersionAsync(settings, request.Version, cancellationToken))
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict);
        }

        return TypedResults.Ok(ToDto(settings));
    }

    private static SettingsDto ToDto(PricingSettings s) => new(
        s.WastePct,
        s.VatPct,
        Money.ToCents(s.MinJobAmount),
        Money.ToCents(s.MaxJobAmount),
        s.PremiumAreaM2,
        s.RangeLowPct,
        s.RangeHighPct,
        s.RangeSizeWideningPct,
        s.RangeDamageWideningPct,
        s.DepositPct,
        s.MinMarginPct,
        s.DefaultHeight,
        s.QuoteValidityDays,
        Money.ToCents(s.SiteVisitPrice),
        s.SiteVisitDeductible,
        s.ReviewDeadlineBusinessDays,
        s.FullRefundMinDays,
        s.LateRefundPct,
        s.MaxPhotosPerElement,
        s.MaxVideosPerElement,
        s.MaxImageSizeMb,
        s.MaxVideoSizeMb,
        s.ClientPhoneVisibleToPainter,
        s.PainterPhoneVisibleToClient,
        s.Version,
        s.UpdatedAt);
}
