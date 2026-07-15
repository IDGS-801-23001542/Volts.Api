using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class CommercialPackage : BaseEntity
{
    public string CommercialPlanId { get; set; } = string.Empty;
    public string CommercialPlanName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal ReferencePrice { get; set; }
    public decimal Savings { get; set; }
    public List<CommercialPackageItem> Items { get; set; } = new();
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CommercialPackageItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}
