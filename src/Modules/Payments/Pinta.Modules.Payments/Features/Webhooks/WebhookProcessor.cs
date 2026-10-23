using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Payments.Contracts;
using Pinta.Modules.Payments.Data;
using Pinta.Modules.Payments.Domain;
using Pinta.Modules.Payments.Features.Pay;
using Pinta.Modules.Payments.Features.Refunds;
using Pinta.Modules.Payments.Stripe;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Events;
using Pinta.SharedKernel.Http;
using Pinta.SharedKernel.Time;
using Stripe;
using DomainRefund = Pinta.Modules.Payments.Domain.Refund;
using PaymentMethod = Pinta.Modules.Payments.Contracts.PaymentMethod;
using StripeRefund = Stripe.Refund;

namespace Pinta.Modules.Payments.Features.Webhooks;

/// <summary>The effects of each Stripe event (S4). Runs inside the webhook's transaction.</summary>
internal sealed class WebhookProcessor(
    PaymentsDbContext db,
    IStripeGateway stripe,
    IPayableJobs jobs,
    IUserDirectory users,
    IEmailQueue emails,
    IEventPublisher events,
    IBackgroundJobClient background,
    WebLinks links,
    TimeProvider clock,
    ILogger<WebhookProcessor> logger)
{
    public Task ProcessAsync(Event stripeEvent, CancellationToken cancellationToken) => stripeEvent.Type switch
    {
        EventTypes.PaymentIntentSucceeded => SucceededAsync((PaymentIntent)stripeEvent.Data.Object, cancellationToken),
        EventTypes.PaymentIntentPaymentFailed => FailedAsync((PaymentIntent)stripeEvent.Data.Object, cancellationToken),
        EventTypes.PaymentIntentRequiresAction => RequiresActionAsync((PaymentIntent)stripeEvent.Data.Object, cancellationToken),
        EventTypes.ChargeRefunded => ChargeRefundedAsync((Charge)stripeEvent.Data.Object, cancellationToken),
        EventTypes.RefundUpdated => RefundUpdatedAsync((StripeRefund)stripeEvent.Data.Object, cancellationToken),
        EventTypes.ChargeDisputeCreated => DisputeAsync((Dispute)stripeEvent.Data.Object, cancellationToken),
        _ => Task.CompletedTask,
    };

    private Task<Payment?> FindAsync(string? paymentIntentId, CancellationToken cancellationToken) =>
        db.Payments.Include(p => p.Refunds).SingleOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId, cancellationToken);

    private async Task SucceededAsync(PaymentIntent intent, CancellationToken cancellationToken)
    {
        var payment = await FindAsync(intent.Id, cancellationToken);
        if (payment is null || payment.Status is PaymentStatus.Succeeded or PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded)
        {
            return;
        }

        var now = clock.GetUtcNow();
        payment.Status = PaymentStatus.Succeeded;
        payment.Method = PaymentRules.FromStripeType(await stripe.GetPaidMethodTypeAsync(intent.Id, cancellationToken)) ?? payment.Method;
        payment.PaidAt = now;
        payment.UpdatedAt = now;
        payment.Error = null;
        payment.ReceiptNumber = await db.NextReceiptNumberAsync(BusinessDays.Today(clock).Year, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var job = await jobs.FindAsync(payment.JobId, cancellationToken);
        if (job is null || !Accepts(job, payment.Type))
        {
            // Paid for a job that can no longer take it (cancelled, expired…): give it all back and tell the admins (S4).
            RefundQueue.Request(db, background, payment, payment.Amount, "automatic: job no longer payable", now);
            await db.SaveChangesAsync(cancellationToken);
            foreach (var admin in await users.ListAdminsAsync(cancellationToken))
            {
                await emails.EnqueueAsync(new AutoRefundEmail(
                    admin.Email, admin.Locale, job?.Code ?? payment.JobId.ToString(), Kind(payment.Type), Money.ToCents(payment.Amount),
                    links.AdminJob(payment.JobId, admin.Locale)) { JobId = payment.JobId }, cancellationToken);
            }

            return;
        }

        await events.PublishAsync(new PaymentSucceeded(payment.Id, payment.JobId, payment.Type, Money.ToCents(payment.Amount)), cancellationToken);
        var client = await users.FindAsync(payment.ClientId, cancellationToken);
        if (client is not null)
        {
            await emails.EnqueueAsync(new PaymentReceivedEmail(
                client.Email, client.Locale, client.Name, job.Code, Kind(payment.Type), Money.ToCents(payment.Amount),
                payment.Method == PaymentMethod.MbWay, links.ClientJob(job.JobId, client.Locale)) { JobId = job.JobId }, cancellationToken);
        }
    }

    /// <summary>Whether the job still takes this payment: a deposit needs the confirmed price, a balance a finished job.</summary>
    private static bool Accepts(PayableJob job, PaymentType type) => type switch
    {
        PaymentType.Deposit => job.Status == JobStatus.Quoted,
        PaymentType.Balance => job.Status == JobStatus.Finished,
        PaymentType.SiteVisit => job.Status is not (JobStatus.Cancelled or JobStatus.Rejected or JobStatus.Expired),
        _ => false,
    };

    private async Task FailedAsync(PaymentIntent intent, CancellationToken cancellationToken)
    {
        var payment = await FindAsync(intent.Id, cancellationToken);
        if (payment is null || payment.Status == PaymentStatus.Succeeded)
        {
            return;
        }

        payment.Status = PaymentStatus.Failed;
        var message = intent.LastPaymentError?.Message;
        payment.Error = message is { Length: > 500 } ? message[..500] : message;
        payment.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Multibanco: the reference the client pays later; the job stays in "Precio confirmado" until it arrives.</summary>
    private async Task RequiresActionAsync(PaymentIntent intent, CancellationToken cancellationToken)
    {
        var details = intent.NextAction?.MultibancoDisplayDetails;
        var payment = await FindAsync(intent.Id, cancellationToken);
        if (payment is null || details is null || payment.Status == PaymentStatus.Succeeded)
        {
            return;
        }

        payment.Status = PaymentStatus.RequiresAction;
        payment.Method = PaymentMethod.Multibanco;
        payment.MultibancoEntity = details.Entity;
        payment.MultibancoReference = details.Reference;
        payment.MultibancoExpiresAt = details.ExpiresAt;
        payment.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        var job = await jobs.FindAsync(payment.JobId, cancellationToken);
        var client = await users.FindAsync(payment.ClientId, cancellationToken);
        if (job is not null && client is not null)
        {
            await emails.EnqueueAsync(new MultibancoReferenceEmail(
                client.Email, client.Locale, client.Name, job.Code, details.Entity ?? "", details.Reference ?? "", Money.ToCents(payment.Amount),
                details.ExpiresAt, links.ClientJob(job.JobId, client.Locale)) { JobId = job.JobId }, cancellationToken);
        }
    }

    /// <summary>The charge was refunded: our pending refunds of that payment are done, up to the amount Stripe reports.</summary>
    private async Task ChargeRefundedAsync(Charge charge, CancellationToken cancellationToken)
    {
        var payment = await FindAsync(charge.PaymentIntentId, cancellationToken);
        if (payment is null)
        {
            return;
        }

        var refunded = Money.FromCents(charge.AmountRefunded);
        var done = payment.Refunds.Where(r => r.Status == RefundStatus.Succeeded).Sum(r => r.Amount);
        foreach (var refund in payment.Refunds.Where(r => r.Status == RefundStatus.Pending).OrderBy(r => r.CreatedAt))
        {
            if (done + refund.Amount > refunded)
            {
                break;
            }

            Complete(refund);
            done += refund.Amount;
        }

        UpdateStatus(payment);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RefundUpdatedAsync(StripeRefund stripeRefund, CancellationToken cancellationToken)
    {
        var refund = await db.Refunds.SingleOrDefaultAsync(r => r.StripeRefundId == stripeRefund.Id, cancellationToken);
        if (refund is null || refund.Status != RefundStatus.Pending)
        {
            return;
        }

        if (stripeRefund.Status == "succeeded")
        {
            Complete(refund);
        }
        else if (stripeRefund.Status is "failed" or "canceled")
        {
            refund.Status = RefundStatus.Failed;
            refund.Error = stripeRefund.FailureReason;
            logger.LogWarning("Stripe refund {RefundId} failed: {Reason}", stripeRefund.Id, stripeRefund.FailureReason);
        }

        var payment = await db.Payments.Include(p => p.Refunds).SingleAsync(p => p.Id == refund.PaymentId, cancellationToken);
        UpdateStatus(payment);
        await db.SaveChangesAsync(cancellationToken);
    }

    private void Complete(DomainRefund refund)
    {
        refund.Status = RefundStatus.Succeeded;
        refund.CompletedAt = clock.GetUtcNow();
    }

    private static void UpdateStatus(Payment payment)
    {
        var refunded = payment.Refunds.Where(r => r.Status == RefundStatus.Succeeded).Sum(r => r.Amount);
        if (refunded > 0)
        {
            payment.Status = refunded >= payment.Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
        }
    }

    private async Task DisputeAsync(Dispute dispute, CancellationToken cancellationToken)
    {
        var payment = await FindAsync(dispute.PaymentIntentId, cancellationToken);
        var job = payment is null ? null : await jobs.FindAsync(payment.JobId, cancellationToken);
        foreach (var admin in await users.ListAdminsAsync(cancellationToken))
        {
            await emails.EnqueueAsync(new PaymentDisputeEmail(
                admin.Email, admin.Locale, job?.Code ?? dispute.PaymentIntentId ?? dispute.Id, dispute.Amount,
                payment is null ? null : links.AdminJob(payment.JobId, admin.Locale)) { JobId = payment?.JobId }, cancellationToken);
        }
    }

    internal static string Kind(PaymentType type) => type switch
    {
        PaymentType.Deposit => "deposit",
        PaymentType.Balance => "balance",
        _ => "site_visit",
    };
}
