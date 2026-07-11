using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Recipe : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public string Notes { get; set; } = string.Empty;

    public decimal EstimatedUnitCost { get; set; }

    public List<RecipeDetail> Details { get; set; } = new();

    public bool IsActive { get; set; } = true;
}

public class RecipeDetail
{
    public string RawMaterialId { get; set; } = string.Empty;

    public string RawMaterialCode { get; set; } = string.Empty;

    public string RawMaterialName { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    /*
     * Cantidad neta requerida para fabricar una unidad.
     */
    public decimal QuantityRequired { get; set; }

    /*
     * Porcentaje adicional considerado por pérdidas
     * normales del proceso.
     *
     * Ejemplo:
     * QuantityRequired = 1
     * WastePercentage = 10
     * Cantidad total calculada = 1.10
     */
    public decimal WastePercentage { get; set; }

    /*
     * Indica si en el futuro este componente puede
     * abastecerse con sobrantes recuperables.
     */
    public bool AcceptsRecoveredWaste { get; set; }

    public decimal EstimatedUnitCost { get; set; }

    public decimal EstimatedSubtotal { get; set; }
}