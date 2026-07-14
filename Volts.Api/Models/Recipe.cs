using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Volts.Api.Models.Enums;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Recipe : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string Notes { get; set; } = string.Empty;
    public decimal EstimatedUnitCost { get; set; }
    [BsonRepresentation(BsonType.String)]
    public RecipeStatus Status { get; set; } = RecipeStatus.Draft;
    public List<RecipeDetail> Details { get; set; } = new();

    // Compatibilidad con consultas existentes.
    public bool IsActive { get; set; }
}

public class RecipeDetail
{
    public string RawMaterialId { get; set; } = string.Empty;
    public string RawMaterialCode { get; set; } = string.Empty;
    public string RawMaterialName { get; set; } = string.Empty;
    public string UnitOfMeasureId { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string UnitSymbol { get; set; } = string.Empty;
    public bool UnitAllowsDecimals { get; set; }
    public int UnitDecimalPlaces { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal QuantityRequired { get; set; }
    public decimal WastePercentage { get; set; }
    public decimal TotalQuantityPerUnit { get; set; }
    public bool AcceptsRecoveredWaste { get; set; }
    public decimal EstimatedUnitCost { get; set; }
    public decimal EstimatedSubtotal { get; set; }
}
