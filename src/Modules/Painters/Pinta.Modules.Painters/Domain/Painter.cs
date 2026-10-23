using Pinta.Modules.Painters.Contracts;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Painters.Domain;

/// <summary>
/// A curated painter (docs/data-model.md, PINTOR). The IBAN is stored encrypted with Data Protection and only its last
/// digits are kept in clear for the masked display.
/// </summary>
public sealed class Painter : AuditedEntity
{
    public Guid UserId { get; init; }

    public required string Name { get; set; }

    public required string Email { get; init; }

    public string? Phone { get; set; }

    public string? TaxId { get; set; }

    public List<string> Specialties { get; set; } = [];

    public List<string> Municipalities { get; set; } = [];

    public string? IbanEncrypted { get; set; }

    public string? IbanLast4 { get; set; }

    public string? Notes { get; set; }

    /// <summary>Internal rating 1–5, admin only (A4.4).</summary>
    public decimal? Rating { get; set; }

    public PainterStatus Status { get; set; }

    public List<PainterDocument> Documents { get; init; } = [];
}

/// <summary>Serialized as <c>insurance</c>, <c>activity</c>, <c>contract</c>, <c>other</c> (A4.3).</summary>
public enum PainterDocumentKind
{
    Insurance,
    Activity,
    Contract,
    Other,
}

/// <summary>An informative document with an optional expiry; it never blocks activation (A4.3).</summary>
public sealed class PainterDocument
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid PainterId { get; init; }

    public Guid FileId { get; init; }

    public PainterDocumentKind Kind { get; init; }

    public DateOnly? ExpiresOn { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
