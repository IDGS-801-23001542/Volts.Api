using System.Text.Json.Serialization;

namespace Volts.Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProductionStatus
{
    Created,
    InProgress,
    Completed,
    Cancelled
}
