namespace Pinta.Modules.Catalog.Domain;

/// <summary>Catalog of extra items the admin adds during review, with client price and painter pay before VAT.</summary>
public sealed class ExtraItem : CatalogEntity
{
    public decimal ClientPrice { get; set; }

    public decimal PainterPay { get; set; }
}
