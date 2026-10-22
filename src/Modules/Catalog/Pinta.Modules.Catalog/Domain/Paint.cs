namespace Pinta.Modules.Catalog.Domain;

public sealed class Paint : CatalogEntity
{
    public decimal PricePerLitre { get; set; }

    public decimal CoverageM2PerLitre { get; set; }

    public int Coats { get; set; }
}
