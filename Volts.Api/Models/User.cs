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

    /*
     * Compatibilidad con los documentos antiguos de MongoDB.
     * Los usuarios existentes tienen una propiedad llamada FullName.
     */
    [BsonElement("FullName")]
    [JsonIgnore]
    public string? LegacyFullName { get; set; }

    /*
     * Se devuelve al frontend, pero no se almacena como una propiedad
     * adicional en MongoDB.
     */
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

    /*
     * Se conserva temporalmente para evitar romper autorización,
     * JWT y consultas existentes.
     */
    public string RoleName { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public UserType UserType { get; set; } = UserType.Customer;

    /*
     * Identificador del Customer, Institution o perfil correspondiente.
     */
    
    public string? ProfileId { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsEmailConfirmed { get; set; } = false;

    public bool TwoFactorEnabled { get; set; } = false;

    [JsonIgnore]
    public string? TwoFactorSecret { get; set; }

    public int FailedLoginAttempts { get; set; } = 0;

    public DateTime? LockoutEnd { get; set; }

    public DateTime? LastLoginAt { get; set; }
}