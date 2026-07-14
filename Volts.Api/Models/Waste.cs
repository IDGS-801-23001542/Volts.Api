using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Volts.Api.Models.Enums;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Waste : BaseEntity
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
    public string? ProductionOrderId { get; set; }
    public string? ProductionFolio { get; set; }
    public decimal QuantityGenerated { get; set; }
    public decimal AvailableQuantity { get; set; }
    [BsonRepresentation(BsonType.String)]
    public WasteClassification Classification { get; set; }
    [BsonRepresentation(BsonType.String)]
    public WasteDestination Destination { get; set; } = WasteDestination.Pending;
    [BsonRepresentation(BsonType.String)]
    public WasteStatus Status { get; set; } = WasteStatus.Available;
    public decimal UnitCost { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal EstimatedRecoveryValue { get; set; }
    public decimal RecoveredValue { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime WasteDate { get; set; } = DateTime.UtcNow;
    public List<WasteDisposition> Dispositions { get; set; } = new();
}

public class WasteDisposition
{
    [BsonRepresentation(BsonType.String)]
    public WasteDestination Action { get; set; }
    public decimal Quantity { get; set; }
    public decimal RecoveredValue { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime DisposedAt { get; set; } = DateTime.UtcNow;
    public string? DisposedBy { get; set; }
}
