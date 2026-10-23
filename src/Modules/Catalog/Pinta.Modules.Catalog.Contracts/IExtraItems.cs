using Pinta.SharedKernel.Text;

namespace Pinta.Modules.Catalog.Contracts;

/// <summary>An active catalog extra item: client price and painter pay before VAT (A1.5).</summary>
public sealed record CatalogExtra(string Key, LocalizedText Name, decimal ClientPrice, decimal PainterPay);

public interface IExtraItems
{
    Task<CatalogExtra?> FindActiveAsync(string key, CancellationToken cancellationToken = default);
}
