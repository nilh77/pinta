using Pinta.SharedKernel.Events;

namespace Pinta.Modules.Jobs.Contracts;

/// <summary>
/// What Payments needs to charge a job (C5.3): who pays, the state, and the amounts of the confirmed price and of the
/// site visit, all VAT included. <see cref="DepositCents"/> and <see cref="BalanceCents"/> are null without a confirmed
/// price.
/// </summary>
public sealed record PayableJob(
    Guid JobId,
    string Code,
    Guid ClientId,
    JobStatus Status,
    DateOnly? QuoteValidUntil,
    long? TotalCents,
    long? DepositCents,
    long? BalanceCents,
    long SiteVisitPriceCents,
    string? TaxId,
    string? CompanyName,
    string AddressLine1,
    string PostalCode,
    string Locality);

public interface IPayableJobs
{
    Task<PayableJob?> FindAsync(Guid jobId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The job was cancelled (C5.6, A3.4). <see cref="DepositRefundCents"/> is what the policy (or the admin's full-refund
/// exception) gives back from the deposit; Payments refunds it through Stripe.
/// </summary>
public sealed record JobCancelled(Guid JobId, long DepositRefundCents, string? Reason) : IIntegrationEvent;
