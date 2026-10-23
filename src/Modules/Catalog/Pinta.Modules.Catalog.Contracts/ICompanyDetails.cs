namespace Pinta.Modules.Catalog.Contracts;

/// <summary>Company data for documents (simulated receipts, A1.6).</summary>
public sealed record CompanyDetails(string Name, string TaxId, string Address);

public interface ICompanyDetails
{
    Task<CompanyDetails> GetAsync(CancellationToken cancellationToken = default);
}
