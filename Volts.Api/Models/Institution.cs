using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Volts.Api.Models.Common;
using Volts.Api.Models.Enums;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Institution : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de institución utilizado por el modelo nuevo.
    /// Se almacena como texto en MongoDB.
    /// </summary>
    [BsonRepresentation(BsonType.String)]
    public InstitutionType? Type { get; set; }

    public string? EducationalLevel { get; set; }

    public int? EstimatedStudents { get; set; }

    public string? Website { get; set; }

    public InstitutionResponsible Responsible { get; set; } = new();

    /// <summary>
    /// Nombre antiguo del responsable.
    /// </summary>
    [BsonElement("ContactName")]
    [JsonIgnore]
    public string? LegacyContactName { get; set; }

    /// <summary>
    /// Nombre del responsable mostrado por la API.
    /// </summary>
    [BsonIgnore]
    public string ContactName
    {
        get
        {
            if (Responsible?.Name != null &&
                !string.IsNullOrWhiteSpace(
                    Responsible.Name.FullName))
            {
                return Responsible.Name.FullName;
            }

            return LegacyContactName?.Trim()
                ?? string.Empty;
        }
        set
        {
            LegacyContactName =
                string.IsNullOrWhiteSpace(value)
                    ? null
                    : value.Trim();
        }
    }

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    /// <summary>
    /// Dirección estructurada utilizada por los registros nuevos.
    /// </summary>
    [BsonElement("StructuredAddress")]
    public Address? StructuredAddress { get; set; }

    /// <summary>
    /// Campo Address antiguo. Puede contener texto, objeto o null.
    /// </summary>
    [BsonElement("Address")]
    [JsonIgnore]
    public BsonValue? LegacyAddressRaw { get; set; }

    /// <summary>
    /// Devuelve la dirección antigua cuando estaba guardada como texto.
    /// </summary>
    [BsonIgnore]
    public string? LegacyAddress
    {
        get
        {
            if (LegacyAddressRaw == null ||
                LegacyAddressRaw.IsBsonNull)
            {
                return null;
            }

            return LegacyAddressRaw.BsonType ==
                   BsonType.String
                ? LegacyAddressRaw.AsString
                : null;
        }
        set
        {
            LegacyAddressRaw =
                string.IsNullOrWhiteSpace(value)
                    ? BsonNull.Value
                    : new BsonString(value.Trim());
        }
    }

    /// <summary>
    /// Propiedad temporal para que el código anterior pueda
    /// seguir utilizando institution.Address.
    /// </summary>
    [BsonIgnore]
    public string? Address
    {
        get => LegacyAddress;
        set => LegacyAddress = value;
    }

    /// <summary>
    /// Dirección estructurada efectiva. Utiliza StructuredAddress
    /// o intenta recuperar un objeto almacenado en Address.
    /// </summary>
    [BsonIgnore]
    public Address? EffectiveStructuredAddress
    {
        get
        {
            if (StructuredAddress != null)
                return StructuredAddress;

            if (LegacyAddressRaw == null ||
                LegacyAddressRaw.BsonType !=
                BsonType.Document)
            {
                return null;
            }

            try
            {
                return BsonSerializer.Deserialize<Address>(
                    LegacyAddressRaw.AsBsonDocument
                );
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Campo InstitutionType antiguo. Puede estar almacenado
    /// como texto, Int32, Int64 o null.
    /// </summary>
    [BsonElement("InstitutionType")]
    [JsonIgnore]
    public BsonValue? LegacyInstitutionTypeRaw
    {
        get;
        set;
    }

    /// <summary>
    /// Representación temporal del tipo antiguo.
    /// </summary>
    [BsonIgnore]
    public string? LegacyInstitutionType
    {
        get
        {
            if (LegacyInstitutionTypeRaw == null ||
                LegacyInstitutionTypeRaw.IsBsonNull)
            {
                return null;
            }

            return LegacyInstitutionTypeRaw.BsonType switch
            {
                BsonType.String =>
                    LegacyInstitutionTypeRaw.AsString,

                BsonType.Int32 =>
                    LegacyInstitutionTypeRaw
                        .AsInt32
                        .ToString(),

                BsonType.Int64 =>
                    LegacyInstitutionTypeRaw
                        .AsInt64
                        .ToString(),

                _ => null
            };
        }
        set
        {
            LegacyInstitutionTypeRaw =
                string.IsNullOrWhiteSpace(value)
                    ? BsonNull.Value
                    : new BsonString(value.Trim());
        }
    }

    /// <summary>
    /// Tipo efectivo de institución, compatible con registros
    /// nuevos y antiguos.
    /// </summary>
    [BsonIgnore]
    public InstitutionType? EffectiveType
    {
        get
        {
            if (Type.HasValue)
                return Type.Value;

            if (LegacyInstitutionTypeRaw == null ||
                LegacyInstitutionTypeRaw.IsBsonNull)
            {
                return null;
            }

            if (LegacyInstitutionTypeRaw.BsonType ==
                BsonType.Int32)
            {
                var number =
                    LegacyInstitutionTypeRaw.AsInt32;

                return Enum.IsDefined(
                    typeof(InstitutionType),
                    number
                )
                    ? (InstitutionType)number
                    : null;
            }

            if (LegacyInstitutionTypeRaw.BsonType ==
                BsonType.Int64)
            {
                var longNumber =
                    LegacyInstitutionTypeRaw.AsInt64;

                if (longNumber < int.MinValue ||
                    longNumber > int.MaxValue)
                {
                    return null;
                }

                var number = (int)longNumber;

                return Enum.IsDefined(
                    typeof(InstitutionType),
                    number
                )
                    ? (InstitutionType)number
                    : null;
            }

            if (LegacyInstitutionTypeRaw.BsonType ==
                BsonType.String)
            {
                var value =
                    LegacyInstitutionTypeRaw.AsString;

                if (Enum.TryParse<InstitutionType>(
                        value,
                        true,
                        out var parsedByName))
                {
                    return parsedByName;
                }

                if (int.TryParse(
                        value,
                        out var parsedNumber) &&
                    Enum.IsDefined(
                        typeof(InstitutionType),
                        parsedNumber))
                {
                    return (InstitutionType)parsedNumber;
                }
            }

            return null;
        }
    }

    public string? UserId { get; set; }

    public bool IsActive { get; set; } = true;
}