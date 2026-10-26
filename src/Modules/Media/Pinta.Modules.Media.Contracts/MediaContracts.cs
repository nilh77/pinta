namespace Pinta.Modules.Media.Contracts;

/// <summary>Serialized as <c>element</c>, <c>job_general</c>, … (docs/api.md, <c>MediaContext</c>).</summary>
public enum MediaContext
{
    Element,
    JobGeneral,
    SiteVisit,
    Result,
    PainterDocument,

    /// <summary>The painter's photos while the job is in progress; the client sees them (P2).</summary>
    Progress,

    /// <summary>Photos of an incident the painter reports; only the admins see them (P2).</summary>
    Incident,
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

/// <summary>A presigned POST to upload one file straight to storage (valid 10 minutes).</summary>
public sealed record SignedUpload(Guid FileId, string Url, IReadOnlyDictionary<string, string> Fields, DateTimeOffset ExpiresAt);

/// <summary>
/// An upload made by a signed-in user for something that already exists: a painter document (<see cref="PainterId"/>)
/// or a job's result photos (<see cref="JobId"/>).
/// </summary>
public sealed record OwnedUploadRequest(
    MediaContext Context, Guid UploadedBy, Guid? JobId, Guid? PainterId, string ContentType, long SizeBytes);

/// <summary>Why an owned upload was refused: a type the context does not take, or a file too large.</summary>
public enum UploadRefusal
{
    UnsupportedType,
    TooLarge,
}

/// <summary>A stored file with a short-lived signed read URL.</summary>
public sealed record StoredFile(Guid Id, MediaContext Context, MediaKind Kind, string ContentType, long SizeBytes, string Url, DateTimeOffset UrlExpiresAt);

/// <summary>Files of a request, used by the Jobs module when the client sends it and to show them.</summary>
public interface IMediaService
{
    /// <summary>
    /// Attaches the confirmed, not yet attached files uploaded with <paramref name="draftId"/> to the job. Element files
    /// get the quote element id of their client key; files whose element no longer exists are left out.
    /// </summary>
    Task<int> AttachDraftFilesAsync(
        Guid draftId, Guid jobId, Guid clientId, IReadOnlyDictionary<string, Guid> elementIds, CancellationToken cancellationToken = default);

    Task<(SignedUpload? Upload, UploadRefusal? Refusal)> CreateOwnedUploadAsync(OwnedUploadRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms files uploaded by <paramref name="uploadedBy"/> for the same owner once they are in storage; returns the
    /// confirmed ones (unknown, foreign or missing files are left out).
    /// </summary>
    Task<IReadOnlyList<StoredFile>> ConfirmOwnedAsync(
        IReadOnlyCollection<Guid> fileIds, Guid uploadedBy, Guid? jobId, Guid? painterId, MediaContext context, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredFile>> FindAsync(IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>The job's confirmed files, oldest first, each with a signed read URL.</summary>
    Task<IReadOnlyList<JobFile>> ListForJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}
