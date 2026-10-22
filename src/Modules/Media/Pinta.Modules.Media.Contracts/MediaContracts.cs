namespace Pinta.Modules.Media.Contracts;

/// <summary>Serialized as <c>element</c>, <c>job_general</c>, … (docs/api.md, <c>MediaContext</c>).</summary>
public enum MediaContext
{
    Element,
    JobGeneral,
    SiteVisit,
    Result,
    PainterDocument,
}

/// <summary>Serialized as <c>image</c>, <c>video</c>, <c>document</c>.</summary>
public enum MediaKind
{
    Image,
    Video,
    Document,
}

/// <summary>Files of a request, used by the Jobs module when the client sends it.</summary>
public interface IMediaService
{
    /// <summary>
    /// Attaches the confirmed, not yet attached files uploaded with <paramref name="draftId"/> to the job. Element files
    /// get the quote element id of their client key; files whose element no longer exists are left out.
    /// </summary>
    Task<int> AttachDraftFilesAsync(
        Guid draftId, Guid jobId, Guid clientId, IReadOnlyDictionary<string, Guid> elementIds, CancellationToken cancellationToken = default);
}
