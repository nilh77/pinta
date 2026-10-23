using Pinta.Modules.Finance.Contracts;

namespace Pinta.Modules.Finance.Domain;

/// <summary>An operating cost of a day (COSTE_OPERACION, A5.2); a closed month keeps it in its closing.</summary>
public sealed class OperatingCost
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateOnly Date { get; set; }

    public OperatingCostCategory Category { get; set; }

    public required string Description { get; set; }

    public decimal Amount { get; set; }

    public Guid? ClosingId { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public Guid CreatedBy { get; init; }

    public uint Version { get; set; }
}

/// <summary>A month closed by the partners (CIERRE_MENSUAL, A5.3): its figures frozen as they were.</summary>
public sealed class MonthlyClosing
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>First day of the month.</summary>
    public DateOnly Period { get; init; }

    public decimal Income { get; init; }

    public decimal Vat { get; init; }

    public decimal Material { get; init; }

    public decimal PainterPay { get; init; }

    public decimal GrossMargin { get; init; }

    public decimal Costs { get; init; }

    public decimal Profit { get; init; }

    public decimal PerPartner { get; init; }

    public DateTimeOffset ClosedAt { get; init; }

    public Guid ClosedBy { get; init; }
}
