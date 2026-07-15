using System.Text.Json.Serialization;

namespace Volts.Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InstitutionType
{
    Kindergarten,
    ElementarySchool,
    MiddleSchool,
    HighSchool,
    University,
    TrainingCenter,
    Association,
    Foundation,
    Government,
    Company,
    Other
}
