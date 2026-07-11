using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class RawMaterial : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal CurrentStock { get; set; }

    public decimal MinimumStock { get; set; }

    public decimal MaximumStock { get; set; }

    public decimal AverageCost { get; set; }

    public decimal LastPurchaseCost { get; set; }

    public bool IsRecycled { get; set; }

    public bool IsReusable { get; set; }

    public bool RequiresPurchase { get; set; } = true;

    public string StorageLocation { get; set; } = string.Empty;

    public string? PreferredSupplierId { get; set; }

    public string? PreferredSupplierName { get; set; }

    public bool IsActive { get; set; } = true;
}