using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Payments.Contracts;
using Pinta.Modules.Payments.Data;
using Pinta.Modules.Payments.Domain;
using Pinta.SharedKernel;

namespace Pinta.Modules.Payments.Features;

internal sealed class JobPayments(PaymentsDbContext db) : IJobPayments
{
    public async Task<long> DepositHeldCentsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var deposits = await db.Payments.AsNoTracking().Include(p => p.Refunds)
            .Where(p => p.JobId == jobId && p.Type == PaymentType.Deposit && p.PaidAt != null)
            .ToListAsync(cancellationToken);
        return Money.ToCents(deposits.Sum(p => p.Amount - p.Refunded));
    }

    public async Task<long> SiteVisitPaidCentsAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        Money.ToCents(await db.Payments.AsNoTracking()
            .Where(p => p.JobId == jobId && p.Type == PaymentType.SiteVisit && p.Status == PaymentStatus.Succeeded)
            .SumAsync(p => p.Amount, cancellationToken));
}
