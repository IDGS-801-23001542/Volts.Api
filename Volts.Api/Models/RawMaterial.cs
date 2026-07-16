using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class RawMaterial : BaseEntity
{
    public string Code { get; set; } =
        string.Empty;

    public string Name { get; set; } =
        string.Empty;

    public string Description { get; set; } =
        string.Empty;

    public string Category { get; set; } =
        string.Empty;

    /*
     * Referencia principal al catálogo normalizado
     * UnitsOfMeasure.
     */
    public string UnitOfMeasureId { get; set; } =
        string.Empty;

    /*
     * Fotografía de la unidad utilizada actualmente.
     *
     * UnitCode:
     * Piece, Kilogram, Meter, etc.
     *
     * UnitSymbol:
     * pza, kg, m, etc.
     */
    public string UnitCode { get; set; } =
        string.Empty;

    public string UnitName { get; set; } =
        string.Empty;

    public string UnitSymbol { get; set; } =
        string.Empty;

    public bool UnitAllowsDecimals { get; set; }

    public int UnitDecimalPlaces { get; set; }

    /*
     * Compatibilidad temporal.
     *
     * Los módulos de Compras, Recetas, Producción y
     * Merma todavía utilizan material.Unit.
     *
     * Mientras esos módulos son migrados, Unit guarda
     * el símbolo normalizado, por ejemplo:
     * pza, kg, m, ml.
     */
    public string Unit { get; set; } =
        string.Empty;

    public decimal CurrentStock { get; set; }

    public decimal MinimumStock { get; set; }

    /*
     * Cero significa que no existe un límite máximo
     * configurado.
     */
    public decimal MaximumStock { get; set; }

    public decimal AverageCost { get; set; }

    public decimal LastPurchaseCost { get; set; }

    public bool IsRecycled { get; set; }

    public bool IsReusable { get; set; }

    public bool RequiresPurchase { get; set; } =
        true;

    public string StorageLocation { get; set; } =
        string.Empty;

    public string? PreferredSupplierId { get; set; }

    public string? PreferredSupplierName { get; set; }

    public bool IsActive { get; set; } = true;
}