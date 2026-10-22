using Pinta.Pricing;

namespace Pinta.Modules.Catalog.Contracts;

/// <summary>The current pricing configuration, frozen as a snapshot: only active catalog items, keyed by catalog key.</summary>
public interface IPriceSnapshotProvider
{
    Task<PriceSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default);
}
