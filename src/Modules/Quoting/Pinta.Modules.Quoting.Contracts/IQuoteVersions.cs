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

/// <summary>Quote versions of a job, each with its frozen price snapshot (docs/data-model.md, COTIZACION).</summary>
public interface IQuoteVersions
{
    /// <summary>Prices the client's elements with the current configuration and stores version 1, origin "estimate".</summary>
    Task<(QuoteVersionSummary? Version, QuoteErrors? Errors)> CreateEstimateAsync(
        Guid jobId, Guid createdBy, Occupancy occupancy, IReadOnlyList<QuoteElementRequest> elements, CancellationToken cancellationToken = default);

    Task<QuoteVersionSummary?> GetAsync(Guid versionId, CancellationToken cancellationToken = default);
}
