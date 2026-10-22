namespace Pinta.Modules.Catalog.Contracts;

/// <summary>File limits of the global settings (A1.6): per element and per file.</summary>
public sealed record UploadLimits(int MaxPhotosPerElement, int MaxVideosPerElement, int MaxImageSizeMb, int MaxVideoSizeMb);

public interface IUploadLimits
{
    Task<UploadLimits> GetAsync(CancellationToken cancellationToken = default);
}
