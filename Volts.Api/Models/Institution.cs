using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Serializers;
using Volts.Api.Models.Common;
using Volts.Api.Models.Enums;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Institution : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public InstitutionType InstitutionType { get; set; }
        = InstitutionType.Other;

    public InstitutionResponsible Responsible { get; set; }
        = new();

    [BsonSerializer(typeof(LegacyAddressSerializer))]
    public Address? Address { get; set; }

    public int? EstimatedStudents { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class LegacyAddressSerializer :
    SerializerBase<Address?>
{
    public override Address? Deserialize(
        BsonDeserializationContext context,
        BsonDeserializationArgs args)
    {
        var bsonType =
            context.Reader.GetCurrentBsonType();

        if (bsonType == BsonType.Null)
        {
            context.Reader.ReadNull();
            return null;
        }

        if (bsonType == BsonType.String)
        {
            var legacyAddress =
                context.Reader.ReadString();

            if (string.IsNullOrWhiteSpace(
                    legacyAddress))
            {
                return null;
            }

            /*
             * Compatibilidad temporal con instituciones
             * antiguas, cuya dirección estaba guardada
             * como un solo texto.
             *
             * El valor se conserva en References para no
             * perder información y permitir que el registro
             * sea editado posteriormente desde el formulario
             * estructurado.
             */
            return new Address
            {
                Street = string.Empty,
                ExteriorNumber = string.Empty,
                InteriorNumber = null,
                Neighborhood = string.Empty,
                PostalCode = string.Empty,
                City = string.Empty,
                State = string.Empty,
                Country = "México",
                References = legacyAddress.Trim()
            };
        }

        if (bsonType == BsonType.Document)
        {
            var serializer =
                BsonSerializer.LookupSerializer<Address>();

            return serializer.Deserialize(
                context,
                args
            );
        }

        /*
         * Si existe un dato antiguo con un tipo inesperado,
         * se omite para impedir que toda la consulta falle.
         */
        context.Reader.SkipValue();

        return null;
    }

    public override void Serialize(
        BsonSerializationContext context,
        BsonSerializationArgs args,
        Address? value)
    {
        if (value == null)
        {
            context.Writer.WriteNull();
            return;
        }

        var serializer =
            BsonSerializer.LookupSerializer<Address>();

        serializer.Serialize(
            context,
            args,
            value
        );
    }
}