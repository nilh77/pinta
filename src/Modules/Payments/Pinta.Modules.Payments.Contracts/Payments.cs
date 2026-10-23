using Pinta.SharedKernel.Events;

namespace Pinta.Modules.Payments.Contracts;

/// <summary>Serialized as <c>deposit</c>, <c>balance</c>, <c>site_visit</c> (docs/api.md, <c>PaymentType</c>).</summary>
public enum PaymentType
{
    Deposit,
    Balance,
    SiteVisit,
}

/// <summary>Serialized as <c>card</c>, <c>mb_way</c>, <c>multibanco</c>.</summary>
public enum PaymentMethod
{
    Card,
    MbWay,
    Multibanco,
}

/// <summary>Serialized as <c>pending</c>, <c>requires_action</c>, <c>succeeded</c>, … (docs/api.md, <c>PaymentStatus</c>).</summary>
public enum PaymentStatus
{
    Pending,
    RequiresAction,
    Succeeded,
    Failed,
    Cancelled,
    Refunded,
    PartiallyRefunded,
}

/// <summary>
/// Stripe confirmed a payment by webhook and the job could take it (S4). Jobs moves the state: a deposit takes a
/// confirmed price to "Señal pagada", a balance takes a finished job to "Cerrado".
/// </summary>
public sealed record PaymentSucceeded(Guid PaymentId, Guid JobId, PaymentType Type, long AmountCents) : IIntegrationEvent;

/// <summary>What other modules may know about a job's payments.</summary>
public interface IJobPayments
{
    /// <summary>The paid deposit still held (paid minus refunded), in cents; 0 when there is none.</summary>
    Task<long> DepositHeldCentsAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The paid deposit Pinta keeps after a cancellation: paid minus every refund decided, completed or not (failed ones
    /// excluded), in cents (A5.1).
    /// </summary>
    Task<long> DepositRetainedCentsAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>A paid site visit, VAT included, in cents; 0 when there is none.</summary>
    Task<long> SiteVisitPaidCentsAsync(Guid jobId, CancellationToken cancellationToken = default);
}
