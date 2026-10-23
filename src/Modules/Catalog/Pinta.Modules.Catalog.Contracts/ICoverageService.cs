namespace Pinta.Modules.Catalog.Contracts;

/// <summary>Service area by postal code (C4.1).</summary>
public interface ICoverageService
{
    /// <summary>The concelho of a covered <c>NNNN-NNN</c> postal code, or null when Pinta does not work there.</summary>
    Task<string?> FindMunicipalityAsync(string postalCode, CancellationToken cancellationToken = default);

    /// <summary>Concelhos with at least one active postal prefix, sorted (painter zones).</summary>
    Task<IReadOnlyList<string>> ListMunicipalitiesAsync(CancellationToken cancellationToken = default);
}

public static class PostalCode
{
    /// <summary>Portuguese postal code format (docs/integrations.md).</summary>
    public const string Pattern = @"^\d{4}-\d{3}$";
}
