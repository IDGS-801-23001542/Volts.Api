using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Recipe : BaseEntity
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public List<RecipeDetail> Details { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class RecipeDetail
{
    public string RawMaterialId { get; set; } = string.Empty;
    public string RawMaterialName { get; set; } = string.Empty;
    public decimal QuantityRequired { get; set; }
    public string Unit { get; set; } = string.Empty;
}