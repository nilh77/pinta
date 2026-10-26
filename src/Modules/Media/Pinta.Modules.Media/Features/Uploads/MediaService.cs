using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Media.Contracts;
using Pinta.Modules.Media.Data;
using Pinta.Modules.Media.Domain;
using Pinta.Modules.Media.Storage;

namespace Pinta.Modules.Media.Features.Uploads;

internal sealed class MediaService(MediaDbContext db, ObjectStorage storage, TimeProvider clock) : IMediaService
{
    public static readonly TimeSpan ReadUrlLifetime = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(10);
    private const long MaxOwnedBytes = 10 * 1024 * 1024;

    /// <summary>Documents take PDFs and images; result photos only images (docs/screens.md, Pintor). Up to 10 MB.</summary>
    private static readonly IReadOnlyDictionary<string, (MediaKind Kind, string Extension)> DocumentTypes = new Dictionary<string, (MediaKind, string)>
    {
        ["application/pdf"] = (MediaKind.Document, "pdf"),
        ["image/jpeg"] = (MediaKind.Image, "jpg"),
        ["image/png"] = (MediaKind.Image, "png"),
        ["image/webp"] = (MediaKind.Image, "webp"),
    };

    public async Task<(SignedUpload? Upload, UploadRefusal? Refusal)> CreateOwnedUploadAsync(OwnedUploadRequest request, CancellationToken cancellationToken = default)
    {
        var contentType = request.ContentType.Trim().ToLowerInvariant();
        if (!DocumentTypes.TryGetValue(contentType, out var type) || (request.Context != MediaContext.PainterDocument && type.Kind != MediaKind.Image))
        {
            return (null, UploadRefusal.UnsupportedType);
        }

        if (request.SizeBytes > MaxOwnedBytes)
        {
            return (null, UploadRefusal.TooLarge);
        }

        var folder = request.PainterId is { } painterId ? $"painters/{painterId:N}" : $"jobs/{request.JobId:N}/{request.Context switch { MediaContext.Result => "result", MediaContext.SiteVisit => "visit", MediaContext.Progress => "progress", MediaContext.Incident => "incident", _ => "files" }}";
        var file = new MediaFile
        {
            UploadedBy = request.UploadedBy,
            JobId = request.JobId,
            PainterId = request.PainterId,
            Context = request.Context,
            Kind = type.Kind,
            ContentType = contentType,
            SizeBytes = request.SizeBytes,
            StorageKey = $"{folder}/{Guid.CreateVersion7():N}.{type.Extension}",
            CreatedAt = clock.GetUtcNow(),
        };
        await storage.EnsureBucketAsync(cancellationToken);
        var upload = storage.CreateUpload(file.StorageKey, contentType, MaxOwnedBytes, UploadLifetime);
        db.Files.Add(file);
        await db.SaveChangesAsync(cancellationToken);
        return (new SignedUpload(file.Id, upload.Url, upload.Fields, upload.ExpiresAt), null);
    }

    public async Task<IReadOnlyList<StoredFile>> ConfirmOwnedAsync(
        IReadOnlyCollection<Guid> fileIds, Guid uploadedBy, Guid? jobId, Guid? painterId, MediaContext context, CancellationToken cancellationToken = default)
    {
        var files = await db.Files
            .Where(f => fileIds.Contains(f.Id) && f.UploadedBy == uploadedBy && f.JobId == jobId && f.PainterId == painterId && f.Context == context)
            .ToListAsync(cancellationToken);
        var confirmed = new List<MediaFile>();
        foreach (var file in files)
        {
            if (!file.Confirmed)
            {
                var stored = await storage.FindAsync(file.StorageKey, cancellationToken);
                if (stored is null || !string.Equals(stored.ContentType, file.ContentType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                file.SizeBytes = stored.SizeBytes;
                file.Confirmed = true;
                file.ConfirmedAt = clock.GetUtcNow();
            }

            confirmed.Add(file);
        }

        await db.SaveChangesAsync(cancellationToken);
        return [.. confirmed.Select(ToStored)];
    }

    public async Task<IReadOnlyList<StoredFile>> FindAsync(IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken = default) =>
        [.. (await db.Files.AsNoTracking().Where(f => fileIds.Contains(f.Id) && f.Confirmed).ToListAsync(cancellationToken)).Select(ToStored)];

    public async Task DeleteAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await db.Files.SingleOrDefaultAsync(f => f.Id == fileId, cancellationToken);
        if (file is null)
        {
            return;
        }

        await storage.DeleteAsync(file.StorageKey, cancellationToken);
        db.Files.Remove(file);
        await db.SaveChangesAsync(cancellationToken);
    }

    private StoredFile ToStored(MediaFile f)
    {
        var (url, expiresAt) = storage.CreateDownload(f.StorageKey, ReadUrlLifetime);
        return new StoredFile(f.Id, f.Context, f.Kind, f.ContentType, f.SizeBytes, url, expiresAt);
    }

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
