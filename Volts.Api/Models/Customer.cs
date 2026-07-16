using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Volts.Api.Models.Common;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Customer : BaseEntity
{
    public PersonName Name { get; set; } = new();

    [BsonElement("LegacyFullName")]
    [JsonIgnore]
    public string? LegacyFullName { get; set; }

    [BsonIgnore]
    public string FullName
    {
        get => Name.HasStructuredName
            ? Name.FullName
            : LegacyFullName ?? string.Empty;
        set => LegacyFullName = string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    /* Compatibilidad temporal con AuthService anterior. */
    [BsonIgnore]
    [JsonIgnore]
    public string CustomerType { get; set; } = "Individual";

    [BsonIgnore]
    [JsonIgnore]
    public string? InstitutionName { get; set; }

    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public Address? Address { get; set; }
    public bool IsActive { get; set; } = true;
}
