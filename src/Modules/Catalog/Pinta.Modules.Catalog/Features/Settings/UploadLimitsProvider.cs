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
            .Select(s => new RequestSettings(s.MinLeadBusinessDays, s.ReviewDeadlineBusinessDays))
            .SingleAsync(cancellationToken);
}
