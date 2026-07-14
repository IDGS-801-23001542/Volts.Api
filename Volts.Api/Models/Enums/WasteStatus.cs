using System.Text.Json.Serialization;

namespace Volts.Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WasteStatus
{
    Available,
    PartiallyDisposed,
    Consumed
}
