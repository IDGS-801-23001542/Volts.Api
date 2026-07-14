using System.Text.Json.Serialization;

namespace Volts.Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WasteDestination
{
    Pending,
    Reuse,
    Sell,
    Recycle,
    Repair,
    Discard
}
