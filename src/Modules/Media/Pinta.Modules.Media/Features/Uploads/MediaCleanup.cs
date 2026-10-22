using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pinta.Modules.Media.Data;
using Pinta.Modules.Media.Storage;

namespace Pinta.Modules.Media.Features.Uploads;

/// <summary>Daily Hangfire job: deletes uploads never confirmed after a day (docs/integrations.md).</summary>
public sealed class MediaCleanup(MediaDbContext db, ObjectStorage storage, TimeProvider clock, ILogger<MediaCleanup> logger)
{
    public const string JobId = "media-cleanup";

    public async Task RunAsync()
    {
        var cutoff = clock.GetUtcNow().AddDays(-1);
        var stale = await db.Files.Where(f => !f.Confirmed && f.CreatedAt < cutoff).ToListAsync();
        foreach (var file in stale)
        {
            await storage.DeleteAsync(file.StorageKey, CancellationToken.None);
            db.Files.Remove(file);
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Deleted {Count} unconfirmed uploads", stale.Count);
    }
}
