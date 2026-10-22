using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Media.Contracts;
using Pinta.Modules.Media.Data;

namespace Pinta.Modules.Media.Features.Uploads;

internal sealed class MediaService(MediaDbContext db) : IMediaService
{
    public async Task<int> AttachDraftFilesAsync(
        Guid draftId, Guid jobId, Guid clientId, IReadOnlyDictionary<string, Guid> elementIds, CancellationToken cancellationToken = default)
    {
        var files = await db.Files
            .Where(f => f.DraftId == draftId && f.JobId == null && f.Confirmed && (f.UploadedBy == null || f.UploadedBy == clientId))
            .ToListAsync(cancellationToken);
        var attached = 0;
        foreach (var file in files)
        {
            if (file.ElementKey is not null)
            {
                if (!elementIds.TryGetValue(file.ElementKey, out var elementId))
                {
                    continue;
                }

                file.QuoteElementId = elementId;
            }

            file.JobId = jobId;
            attached++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return attached;
    }
}
