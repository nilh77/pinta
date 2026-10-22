namespace Pinta.Modules.Catalog.Contracts;

/// <summary>
/// Global settings that apply to a client request (A1.6): date notice and review deadline in working days, how long a
/// confirmed price is valid and the site visit price (euros with VAT, deductible from the job or not).
/// </summary>
public sealed record RequestSettings(
    int MinLeadBusinessDays, int ReviewDeadlineBusinessDays, int QuoteValidityDays, decimal SiteVisitPrice, bool SiteVisitDeductible);

public interface IRequestSettings
{
    Task<RequestSettings> GetAsync(CancellationToken cancellationToken = default);
}
