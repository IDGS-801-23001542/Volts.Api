using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class EtlLog : BaseEntity
{
    public string ProcessName { get; set; } = string.Empty;
    public string Source { get; set; } = "MongoDB";
    public string Destination { get; set; } = "Analytics";
    public string Status { get; set; } = "Running";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    public int RecordsRead { get; set; }
    public int RecordsProcessed { get; set; }
    public int RecordsRejected { get; set; }

    public List<string> Phases { get; set; } = new();
    public List<string> Findings { get; set; } = new();

    public string? ErrorMessage { get; set; }
}
