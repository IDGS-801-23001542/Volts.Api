using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Volts.Api.Models.Common;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Customer : BaseEntity
{
    /// <summary>
    /// Nombre estructurado utilizado por el nuevo modelo.
    /// </summary>
    public PersonName Name { get; set; } = new();

    /// <summary>
    /// Campo antiguo almacenado como FullName en MongoDB.
    /// Se conserva mientras se migran los documentos existentes.
    /// </summary>
    [BsonElement("FullName")]
    [JsonIgnore]
    public string? LegacyFullName { get; set; }

    /// <summary>
    /// Nombre completo enviado al frontend.
    /// No se almacena como campo adicional en MongoDB.
    /// </summary>
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

    public string? Phone { get; set; }

    /// <summary>
    /// Nueva dirección estructurada.
    /// Se utiliza otro nombre BSON para evitar conflictos con el campo
    /// Address antiguo, que está almacenado como texto.
    /// </summary>
    [BsonElement("StructuredAddress")]
    public Address? StructuredAddress { get; set; }

    /// <summary>
    /// Campo Address antiguo. Se mantiene como BsonValue porque en MongoDB
    /// existen documentos donde Address es texto y otros donde es un objeto.
    /// </summary>
    [BsonElement("Address")]
    [JsonIgnore]
    public BsonValue? LegacyAddressRaw { get; set; }

    /// <summary>
    /// Dirección antigua cuando Address está almacenado como texto.
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

            return LegacyAddressRaw.BsonType == BsonType.String
                ? LegacyAddressRaw.AsString
                : null;
        }
        set
        {
            LegacyAddressRaw = string.IsNullOrWhiteSpace(value)
                ? BsonNull.Value
                : new BsonString(value.Trim());
        }
    }

    /// <summary>
    /// Propiedad puente para el código anterior.
    /// </summary>
    [BsonIgnore]
    public string? Address
    {
        get => LegacyAddress;
        set => LegacyAddress = value;
    }

    /// <summary>
    /// Relación opcional con el usuario que inicia sesión.
    /// </summary>
    public string? UserId { get; set; }

    public bool IsActive { get; set; } = true;

    /*
     * Campos temporales conservados para no romper documentos
     * o interfaces anteriores.
     */

    public string CustomerType { get; set; } = "Individual";

    public string? InstitutionName { get; set; }
}