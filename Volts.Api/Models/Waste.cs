using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Waste : BaseEntity
{
    public string RawMaterialId { get; set; } =
        string.Empty;

    public string RawMaterialCode { get; set; } =
        string.Empty;

    public string RawMaterialName { get; set; } =
        string.Empty;

    public string Unit { get; set; } =
        string.Empty;

    public string? ProductionOrderId { get; set; }

    public string? ProductionFolio { get; set; }

    public decimal QuantityGenerated { get; set; }

    /*
     * Cantidad que todavía puede reutilizarse,
     * venderse, reciclarse o desecharse.
     */
    public decimal AvailableQuantity { get; set; }

    public string Classification { get; set; } =
        "Reusable";

    public string Destination { get; set; } =
        "Pending";

    public string Status { get; set; } =
        "Available";

    public decimal UnitCost { get; set; }

    public decimal EstimatedCost { get; set; }

    public decimal EstimatedRecoveryValue { get; set; }

    public decimal RecoveredValue { get; set; }

    public string Reason { get; set; } =
        string.Empty;

    public string Notes { get; set; } =
        string.Empty;

    public DateTime WasteDate { get; set; } =
        DateTime.UtcNow;
}