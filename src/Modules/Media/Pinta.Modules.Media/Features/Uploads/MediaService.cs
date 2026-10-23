using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Media.Contracts;
using Pinta.Modules.Media.Data;
using Pinta.Modules.Media.Storage;

namespace Pinta.Modules.Media.Features.Uploads;

internal sealed class MediaService(MediaDbContext db, ObjectStorage storage) : IMediaService
{
    public static readonly TimeSpan ReadUrlLifetime = TimeSpan.FromMinutes(15);

    public async Task<IReadOnlyList<JobFile>> ListForJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var files = await db.Files.AsNoTracking()
            .Where(f => f.JobId == jobId && f.Confirmed)
            .OrderBy(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
        return files.Select(f =>
        {
            var (url, expiresAt) = storage.CreateDownload(f.StorageKey, ReadUrlLifetime);
            return new JobFile(f.Id, f.QuoteElementId, f.ElementKey, f.Context, f.Kind, f.ContentType, f.SizeBytes, url, expiresAt);
        }).ToList();
    }

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
