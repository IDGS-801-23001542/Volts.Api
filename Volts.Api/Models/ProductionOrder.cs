using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class ProductionOrder : BaseEntity
{
    public string Folio { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string RecipeId { get; set; } = string.Empty;

    public string RecipeCode { get; set; } = string.Empty;

    public int RecipeVersion { get; set; }

    public int QuantityPlanned { get; set; }

    public int QuantityCompleted { get; set; }

    public int QuantityDefective { get; set; }

    public string Status { get; set; } = "Created";

    public decimal EstimatedMaterialCost { get; set; }

    public decimal ActualMaterialCost { get; set; }

    public List<ProductionMaterial> Materials { get; set; } =
        new();

    public string Notes { get; set; } = string.Empty;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? CancelledAt { get; set; }
}

public class ProductionMaterial
{
    public string RawMaterialId { get; set; } =
        string.Empty;

    public string RawMaterialCode { get; set; } =
        string.Empty;

    public string RawMaterialName { get; set; } =
        string.Empty;

    public string Unit { get; set; } =
        string.Empty;

    public decimal QuantityPerUnit { get; set; }

    public decimal WastePercentage { get; set; }

    public decimal RequiredQuantity { get; set; }

    public decimal IssuedQuantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal TotalCost { get; set; }
}