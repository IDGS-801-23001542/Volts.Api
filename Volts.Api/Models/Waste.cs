using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Waste : BaseEntity
{
    public string RawMaterialId { get; set; } = string.Empty;
    public string RawMaterialName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
}