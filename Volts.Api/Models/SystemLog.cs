using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class SystemLog : BaseEntity
{
    public string Level { get; set; } = "Information";
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public DateTime EventDate { get; set; } = DateTime.UtcNow;
}