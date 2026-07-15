using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Purchase : BaseEntity
{
    public string Folio { get; set; } =
        string.Empty;

    public string? InvoiceNumber { get; set; }

    public string SupplierId { get; set; } =
        string.Empty;

    public string SupplierCode { get; set; } =
        string.Empty;

    public string SupplierName { get; set; } =
        string.Empty;

    public DateTime PurchaseDate { get; set; } =
        DateTime.UtcNow;

    public decimal Subtotal { get; set; }

    public decimal Tax { get; set; }

    public decimal ShippingCost { get; set; }

    public decimal Total { get; set; }

    public string Status { get; set; } =
        "Completed";

    public string Notes { get; set; } =
        string.Empty;

    public List<PurchaseDetail> Details
    {
        get;
        set;
    } = new();
}

public class PurchaseDetail
{
    public string RawMaterialId { get; set; } =
        string.Empty;

    public string RawMaterialCode { get; set; } =
        string.Empty;

    public string RawMaterialName { get; set; } =
        string.Empty;

    public string UnitOfMeasureId { get; set; } =
        string.Empty;

    public string UnitCode { get; set; } =
        string.Empty;

    public string UnitName { get; set; } =
        string.Empty;

    public string UnitSymbol { get; set; } =
        string.Empty;

    public bool UnitAllowsDecimals { get; set; }

    public int UnitDecimalPlaces { get; set; }

    /*
     * Compatibilidad temporal.
     */
    public string Unit { get; set; } =
        string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal Subtotal { get; set; }

    public decimal PreviousStock { get; set; }

    public decimal NewStock { get; set; }

    public decimal PreviousAverageCost
    {
        get;
        set;
    }

    public decimal NewAverageCost { get; set; }
}