using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Contracts;
using Pinta.Modules.Catalog.Data;

namespace Pinta.Modules.Catalog.Features.Settings;

internal sealed class UploadLimitsProvider(CatalogDbContext db) : IUploadLimits
{
    public Task<UploadLimits> GetAsync(CancellationToken cancellationToken = default) =>
        db.Settings.AsNoTracking()
            .Select(s => new UploadLimits(s.MaxPhotosPerElement, s.MaxVideosPerElement, s.MaxImageSizeMb, s.MaxVideoSizeMb))
            .SingleAsync(cancellationToken);
}

internal sealed class RequestSettingsProvider(CatalogDbContext db) : IRequestSettings
{
    public Task<RequestSettings> GetAsync(CancellationToken cancellationToken = default) =>
        db.Settings.AsNoTracking()
            .Select(s => new RequestSettings(s.MinLeadBusinessDays, s.ReviewDeadlineBusinessDays, s.QuoteValidityDays, s.SiteVisitPrice, s.SiteVisitDeductible))
            .SingleAsync(cancellationToken);
}

internal sealed class ExtraItemsProvider(CatalogDbContext db) : IExtraItems
{
    public async Task<CatalogExtra?> FindActiveAsync(string key, CancellationToken cancellationToken = default) =>
        await db.ExtraItems.AsNoTracking()
            .Where(x => x.Key == key && x.IsActive)
            .Select(x => new CatalogExtra(x.Key, x.Name, x.ClientPrice, x.PainterPay))
            .SingleOrDefaultAsync(cancellationToken);
}
