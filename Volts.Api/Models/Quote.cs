using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Quote : BaseEntity
{
    public string Folio { get; set; } = string.Empty;
    public string RecipientType { get; set; } = "Customer";

    public string? CustomerId { get; set; }
    public string? InstitutionId { get; set; }

    public string RecipientName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }

    public string CommercialPlanId { get; set; } = string.Empty;
    public string CommercialPlanName { get; set; } = string.Empty;
    public string CommercialPackageId { get; set; } = string.Empty;
    public string CommercialPackageName { get; set; } = string.Empty;

    public int PackageQuantity { get; set; }
    public decimal PackageUnitPrice { get; set; }

    public List<QuoteDetail> Details { get; set; } = new();

    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxRate { get; set; } = 0.16m;
    public decimal Tax { get; set; }
    public decimal Shipping { get; set; }
    public decimal Total { get; set; }

    public DateTime ValidUntil { get; set; }
    public string? Notes { get; set; }
    public string? Conditions { get; set; }

    public string Status { get; set; } = "Pending";
    public string? ConvertedOrderId { get; set; }
}

public class QuoteDetail
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int QuantityPerPackage { get; set; }
    public int TotalQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}
