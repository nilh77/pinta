using Hangfire;
using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Payments.Data;
using Pinta.Modules.Payments.Domain;
using Pinta.Modules.Payments.Stripe;
using Pinta.SharedKernel;

namespace Pinta.Modules.Payments.Features.Refunds;

internal static class RefundQueue
{
    /// <summary>Records a refund and asks Hangfire to send it to Stripe once the caller has saved.</summary>
    public static Refund Request(PaymentsDbContext db, IBackgroundJobClient background, Payment payment, decimal amount, string? reason, DateTimeOffset now)
    {
        var refund = new Refund { PaymentId = payment.Id, Amount = amount, Reason = reason, Status = RefundStatus.Pending, CreatedAt = now };
        payment.Refunds.Add(refund);
        db.Refunds.Add(refund);
        background.Enqueue<RefundSender>(sender => sender.SendAsync(refund.Id));
        return refund;
    }
}

/// <summary>Hangfire job: asks Stripe for the refund (idempotent by refund id), retried on failure.</summary>
public sealed class RefundSender(PaymentsDbContext db, IStripeGateway stripe)
{
    [AutomaticRetry(Attempts = 5)]
    public async Task SendAsync(Guid refundId)
    {
        var refund = await db.Refunds.SingleOrDefaultAsync(r => r.Id == refundId);
        if (refund is null || refund.StripeRefundId is not null)
        {
            return;
        }

        var payment = await db.Payments.SingleAsync(p => p.Id == refund.PaymentId);
        refund.StripeRefundId = await stripe.RefundAsync(
            payment.StripePaymentIntentId, Money.ToCents(refund.Amount),
            new Dictionary<string, string> { ["refundId"] = refund.Id.ToString(), ["paymentId"] = payment.Id.ToString() },
            $"refund-{refund.Id}", CancellationToken.None);
        await db.SaveChangesAsync();
    }
}
