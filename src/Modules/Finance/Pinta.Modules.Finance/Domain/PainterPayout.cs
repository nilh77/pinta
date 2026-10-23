using Pinta.Modules.Finance.Contracts;

namespace Pinta.Modules.Finance.Domain;

/// <summary>What Pinta owes a painter for a closed job (PAGO_PINTOR, A5.4): one per job, marked paid with a reference in H7.</summary>
public sealed class PainterPayout
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid PainterId { get; init; }

    public Guid JobId { get; init; }

    public required string JobCode { get; init; }

    public decimal Amount { get; init; }

    public PayoutStatus Status { get; set; }

    public string? Reference { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public uint Version { get; set; }
}
