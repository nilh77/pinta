namespace Pinta.Modules.Jobs.Contracts;

/// <summary>
/// A job that counts in a month's figures (A5.1): closed in the period with its confirmed version's amounts, or
/// cancelled in it with the deposit Pinta kept. Cents; <see cref="At"/> is when it closed or was cancelled.
/// </summary>
public sealed record FinanceJob(
    Guid JobId, string Code, JobStatus Status, DateTimeOffset At, long SubtotalCents, long VatCents, long TotalCents, long MaterialCents,
    long PainterPayCents, long RetainedDepositCents);

/// <summary>What Finance needs from Jobs to build a month (A5.1, A5.3).</summary>
public interface IFinanceJobs
{
    /// <summary>Jobs closed in [from, to), and jobs cancelled in it that kept part of a paid deposit.</summary>
    Task<IReadOnlyList<FinanceJob>> InPeriodAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}
