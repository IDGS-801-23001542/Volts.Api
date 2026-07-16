using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Sale : BaseEntity
{
    public string Folio { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string OrderFolio { get; set; } = string.Empty;
    public string QuoteId { get; set; } = string.Empty;
    public string QuoteFolio { get; set; } = string.Empty;

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

    public DateTime SaleDate { get; set; } = DateTime.UtcNow;
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Shipping { get; set; }
    public decimal Total { get; set; }

    public List<SaleDetail> Details { get; set; } = new();
    public List<string> LicenseIds { get; set; } = new();
}

public class SaleDetail
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}
