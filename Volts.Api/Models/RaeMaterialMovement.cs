using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class RawMaterialMovement : BaseEntity
{
    public string RawMaterialId { get; set; } = string.Empty;

    public string RawMaterialCode { get; set; } = string.Empty;

    public string RawMaterialName { get; set; } = string.Empty;

    public string MovementType { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal PreviousStock { get; set; }

    public decimal NewStock { get; set; }

    public string Unit { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string ReferenceType { get; set; } = "Manual";

    public string? ReferenceId { get; set; }

    public decimal UnitCost { get; set; }

    public decimal TotalCost { get; set; }

    public DateTime MovementDate { get; set; } = DateTime.UtcNow;
}