using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Jobs.Contracts;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Payments.Contracts;
using Pinta.Modules.Payments.Data;
using Pinta.Modules.Payments.Stripe;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Events;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Payments.Features.Refunds;

/// <summary>
/// A cancelled job (C5.6, A3.4): the deposit refund decided by Jobs goes to Stripe through Hangfire and the client gets
/// the email; payments still open can no longer be paid.
/// </summary>
internal sealed class RefundOnCancellation(
    PaymentsDbContext db,
    IStripeGateway stripe,
    IBackgroundJobClient background,
    IPayableJobs jobs,
    IUserDirectory users,
    IEmailQueue emails,
    WebLinks links,
    TimeProvider clock,
    ILogger<RefundOnCancellation> logger) : IIntegrationEventHandler<JobCancelled>
{
    public async Task HandleAsync(JobCancelled cancelled, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var payments = await db.Payments.Include(p => p.Refunds).Where(p => p.JobId == cancelled.JobId).ToListAsync(cancellationToken);
        foreach (var open in payments.Where(p => p.IsOpen))
        {
            await stripe.CancelPaymentIntentAsync(open.StripePaymentIntentId, cancellationToken);
            open.Status = PaymentStatus.Cancelled;
            open.UpdatedAt = now;
        }

        var left = Money.FromCents(cancelled.DepositRefundCents);
        foreach (var deposit in payments.Where(p => p.Type == PaymentType.Deposit && p.PaidAt != null).OrderBy(p => p.PaidAt))
        {
            var amount = Math.Min(left, deposit.Amount - deposit.Refunded);
            if (amount <= 0)
            {
                continue;
            }

            RefundQueue.Request(db, background, deposit, amount, cancelled.Reason ?? "cancellation", now);
            left -= amount;
        }

        if (left > 0)
        {
            logger.LogWarning("Job {JobId}: {Left} € of the refund had no deposit to come from", cancelled.JobId, left);
        }

        await db.SaveChangesAsync(cancellationToken);

        var refunded = cancelled.DepositRefundCents - Money.ToCents(left);
        var job = await jobs.FindAsync(cancelled.JobId, cancellationToken);
        var client = job is null ? null : await users.FindAsync(job.ClientId, cancellationToken);
        if (refunded > 0 && client is not null)
        {
            await emails.EnqueueAsync(new RefundEmail(
                client.Email, client.Locale, client.Name, job!.Code, refunded, links.ClientJob(job.JobId, client.Locale)) { JobId = job.JobId },
                cancellationToken);
        }
    }
}
