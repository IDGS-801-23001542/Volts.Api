using System.Text.Json.Serialization;

namespace Volts.Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecipeStatus
{
    Draft,
    Active,
    Archived
}
