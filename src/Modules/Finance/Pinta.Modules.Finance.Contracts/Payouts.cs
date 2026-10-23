namespace Pinta.Modules.Finance.Contracts;

/// <summary>Serialized as <c>pending</c>, <c>paid</c> (docs/api.md, <c>PayoutStatus</c>).</summary>
public enum PayoutStatus
{
    Pending,
    Paid,
}

/// <summary>
/// The closed list of the partners' agreement (docs/data-model.md): <c>infrastructure</c>, <c>payment_fees</c> (Stripe's
/// included), <c>accounting</c>, <c>insurance</c>, <c>marketing</c>.
/// </summary>
public enum OperatingCostCategory
{
    Infrastructure,
    PaymentFees,
    Accounting,
    Insurance,
    Marketing,
}
