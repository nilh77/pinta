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

/// <summary>A file of a job with a short-lived signed read URL (C5.5, A2.2).</summary>
public sealed record JobFile(
    Guid Id, Guid? QuoteElementId, string? ElementKey, MediaContext Context, MediaKind Kind, string ContentType, long SizeBytes,
    string Url, DateTimeOffset UrlExpiresAt);

/// <summary>Files of a request, used by the Jobs module when the client sends it and to show them.</summary>
public interface IMediaService
{
    /// <summary>
    /// Attaches the confirmed, not yet attached files uploaded with <paramref name="draftId"/> to the job. Element files
    /// get the quote element id of their client key; files whose element no longer exists are left out.
    /// </summary>
    Task<int> AttachDraftFilesAsync(
        Guid draftId, Guid jobId, Guid clientId, IReadOnlyDictionary<string, Guid> elementIds, CancellationToken cancellationToken = default);

    /// <summary>The job's confirmed files, oldest first, each with a signed read URL.</summary>
    Task<IReadOnlyList<JobFile>> ListForJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}
