using System.Text.Json.Serialization;

namespace Volts.Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WasteClassification
{
    Reusable,
    Recyclable,
    Sellable,
    Rework,
    FinalWaste
}
