using Pinta.Modules.Media.Contracts;

namespace Pinta.Modules.Media.Domain;

/// <summary>
/// A photo, video or document in object storage (docs/data-model.md, ARCHIVO). Client uploads start anonymous, tied to
/// the draft's random id, and get the job id when the request is sent.
/// </summary>
public sealed class MediaFile
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid DraftId { get; init; }

    public Guid? UploadedBy { get; init; }

    public Guid? JobId { get; set; }

    /// <summary>The builder's element id while drafting; replaced by the quote element id when attached.</summary>
    public string? ElementKey { get; init; }

    public Guid? QuoteElementId { get; set; }

    public MediaContext Context { get; init; }

    public MediaKind Kind { get; init; }

    public required string ContentType { get; init; }

    public long SizeBytes { get; set; }

    public required string StorageKey { get; init; }

    public bool Confirmed { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ConfirmedAt { get; set; }
}
