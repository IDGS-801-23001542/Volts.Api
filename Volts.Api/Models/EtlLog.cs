using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class EtlLog : BaseEntity
{
    public string ProcessName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = "Running";
    public int RecordsProcessed { get; set; }
    public string? ErrorMessage { get; set; }
}