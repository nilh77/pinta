using Pinta.Modules.Payments.Contracts;

namespace Pinta.Modules.Payments.Domain;

/// <summary>
/// One charge of a job (docs/data-model.md, PAGO): deposit, balance or site visit, each with its own Stripe
/// PaymentIntent. Paid only when Stripe's webhook says so (S4).
/// </summary>
public sealed class Payment
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid JobId { get; init; }

    public Guid ClientId { get; init; }

    public PaymentType Type { get; init; }

    /// <summary>Euros, VAT included.</summary>
    public decimal Amount { get; init; }

    public PaymentStatus Status { get; set; }

    public PaymentMethod? Method { get; set; }

    public required string StripePaymentIntentId { get; init; }

    public string? MultibancoEntity { get; set; }

    public string? MultibancoReference { get; set; }

    public DateTimeOffset? MultibancoExpiresAt { get; set; }

    /// <summary>Stripe's message for the last failure, shown to the client.</summary>
    public string? Error { get; set; }

    /// <summary><c>SIM-YYYY-NNNN</c>, given when the payment succeeds (simulated receipt).</summary>
    public string? ReceiptNumber { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    public uint Version { get; private set; }

    public List<Refund> Refunds { get; init; } = [];

    public bool IsOpen => Status is PaymentStatus.Pending or PaymentStatus.RequiresAction or PaymentStatus.Failed;

    public decimal Refunded => Refunds.Where(r => r.Status != RefundStatus.Failed).Sum(r => r.Amount);
}

public enum RefundStatus
{
    Pending,
    Succeeded,
    Failed,
}

/// <summary>A refund of a payment (docs/data-model.md, REEMBOLSO): requested to Stripe, completed by webhook.</summary>
public sealed class Refund
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid PaymentId { get; init; }

    public decimal Amount { get; init; }

    public string? Reason { get; init; }

    public RefundStatus Status { get; set; }

    public string? StripeRefundId { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>A processed Stripe event (EVENTO_STRIPE): its id as key, so a repeated webhook is detected and ignored.</summary>
public sealed class StripeEvent
{
    public required string Id { get; init; }

    public required string Type { get; init; }

    public required string Payload { get; init; }

    public DateTimeOffset ProcessedAt { get; init; }
}

public sealed class ReceiptCounter
{
    public int Year { get; init; }

    public int Last { get; set; }
}
