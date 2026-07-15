using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Notification : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public string Type { get; set; } = "Information";
    public string Priority { get; set; } = "Normal";
    public string Module { get; set; } = "General";

    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? EntityFolio { get; set; }
    public string? Route { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
