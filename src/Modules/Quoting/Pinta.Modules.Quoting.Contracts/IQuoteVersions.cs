namespace Pinta.Modules.Quoting.Contracts;

/// <summary>A stored quote version as the client may see it: areas, range and flags, never internal amounts.</summary>
public sealed record QuoteVersionSummary(
    Guid Id,
    int Number,
    decimal AreaM2,
    long TotalCents,
    long? RangeMinCents,
    long? RangeMaxCents,
    bool MinimumApplied,
    bool OverMax,
    bool Premium,
    // Quote element id by the builder's QuoteElementRequest.ClientKey.
    IReadOnlyDictionary<string, Guid> ElementIdsByClientKey);

/// <summary>Validation errors by JSON path (<c>elements[0].walls</c>), already translated.</summary>
public sealed record QuoteErrors(IDictionary<string, string[]> Errors);

/// <summary>
/// Quote versions of a job, each with its frozen price snapshot (docs/data-model.md, COTIZACION). Version 1 is the
/// client's estimate; the admin works on a draft (a version not confirmed yet) priced with the snapshot taken when the
/// draft starts, and confirming it fixes the price and its validity.
/// </summary>
public interface IQuoteVersions
{
    /// <summary>Prices the client's elements with the current configuration and stores version 1, origin "estimate".</summary>
    Task<(QuoteVersionSummary? Version, QuoteErrors? Errors)> CreateEstimateAsync(
        Guid jobId, Guid createdBy, Occupancy occupancy, IReadOnlyList<QuoteElementRequest> elements, CancellationToken cancellationToken = default);

    Task<QuoteVersionSummary?> GetAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<AdminQuote?> GetAdminAsync(Guid versionId, CancellationToken cancellationToken = default);

    /// <summary>The job's open draft, or null when there is none.</summary>
    Task<AdminQuote?> GetDraftAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The open draft, or a new one copied from the latest version and priced with the current configuration.
    /// </summary>
    Task<AdminQuote> OpenDraftAsync(Guid jobId, QuoteOrigin origin, Guid createdBy, CancellationToken cancellationToken = default);

    /// <summary>Replaces the draft's contents (opening it first if needed) and reprices it with its own snapshot.</summary>
    Task<(AdminQuote? Quote, QuoteErrors? Errors)> SaveDraftAsync(
        Guid jobId, QuoteOrigin origin, Guid userId, DraftQuoteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms the draft (opening one if there is none): valid until today + the validity days (Lisbon). A margin under
    /// the minimum needs <paramref name="acknowledgeLowMargin"/>; otherwise nothing changes and the result is null.
    /// </summary>
    Task<ConfirmedQuote?> ConfirmDraftAsync(
        Guid jobId, QuoteOrigin origin, Guid userId, string? note, bool acknowledgeLowMargin, CancellationToken cancellationToken = default);

    Task<ClientQuote?> GetClientAsync(Guid versionId, string language, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuoteVersionListItem>> ListAsync(Guid jobId, CancellationToken cancellationToken = default);
}
