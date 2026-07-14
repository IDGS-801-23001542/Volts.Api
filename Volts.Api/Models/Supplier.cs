using MongoDB.Bson.Serialization.Attributes;
using Volts.Api.Models.Common;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Supplier : BaseEntity
{
    public string Code { get; set; } =
        string.Empty;

    public string Name { get; set; } =
        string.Empty;

    public string LegalName { get; set; } =
        string.Empty;

    public string TaxId { get; set; } =
        string.Empty;

    public string ContactName { get; set; } =
        string.Empty;

    public string Email { get; set; } =
        string.Empty;

    public string? Phone { get; set; }

    /*
     * Dirección estructurada.
     *
     * Se utiliza el modelo común ya existente para
     * proveedores, clientes, instituciones y usuarios.
     */
    public Address Address { get; set; } =
        new();

    public string SupplierType { get; set; } =
        "General";

    public List<string> MaterialCategories
    {
        get;
        set;
    } = new();

    public int LeadTimeDays { get; set; }

    public string PaymentTerms { get; set; } =
        string.Empty;

    public string Notes { get; set; } =
        string.Empty;

    public bool IsActive { get; set; } = true;
}