using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Volts.Api.Models.Common;
using Volts.Api.Models.Enums;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class User : BaseEntity
{
    public PersonName Name { get; set; } = new();

    [BsonElement("FullName")]
    [JsonIgnore]
    public string? LegacyFullName { get; set; }

    [BsonIgnore]
    public string FullName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Name.FullName))
                return Name.FullName;

            return LegacyFullName?.Trim() ?? string.Empty;
        }
        set
        {
            LegacyFullName = value?.Trim();
        }
    }

    public string Email { get; set; } = string.Empty;

    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;

    public string RoleId { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public UserType UserType { get; set; } = UserType.Customer;

    public string? ProfileId { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsEmailConfirmed { get; set; } = false;

    public bool TwoFactorEnabled { get; set; } = false;

    [JsonIgnore]
    public string? TwoFactorSecret { get; set; }

    public bool MustChangePassword { get; set; } = false;

    public int FailedLoginAttempts { get; set; } = 0;

    public DateTime? LockoutEnd { get; set; }

    public DateTime? LastLoginAt { get; set; }

    [BsonIgnore]
    public string AccountCategory =>
        UserType == UserType.Employee
            ? "Internal"
            : "Portal";

    [BsonIgnore]
    public string? RelatedProfileName { get; set; }

    [BsonIgnore]
    public string? RelatedProfileType { get; set; }
}
