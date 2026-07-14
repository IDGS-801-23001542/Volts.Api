using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class RawMaterialMovement : BaseEntity
{
    public string RawMaterialId { get; set; } =
        string.Empty;

    public string RawMaterialCode { get; set; } =
        string.Empty;

    public string RawMaterialName { get; set; } =
        string.Empty;

    /*
     * Valores permitidos en este bloque:
     *
     * Entry
     * Exit
     *
     * Otros módulos podrán guardar posteriormente:
     *
     * PurchaseEntry
     * ProductionIssue
     * Waste
     * WasteRecovery
     *
     * El controlador manual de materia prima solamente
     * acepta Entry y Exit.
     */
    public string MovementType { get; set; } =
        string.Empty;

    public decimal Quantity { get; set; }

    public decimal PreviousStock { get; set; }

    public decimal NewStock { get; set; }

    /*
     * Fotografía histórica de la unidad.
     *
     * Aunque la unidad sea renombrada después, el
     * movimiento conserva cómo fue registrada.
     */
    public string UnitOfMeasureId { get; set; } =
        string.Empty;

    public string UnitCode { get; set; } =
        string.Empty;

    public string UnitName { get; set; } =
        string.Empty;

    public string UnitSymbol { get; set; } =
        string.Empty;

    public bool UnitAllowsDecimals { get; set; }

    public int UnitDecimalPlaces { get; set; }

    /*
     * Compatibilidad temporal con frontend y módulos
     * que todavía esperan la propiedad Unit.
     */
    public string Unit { get; set; } =
        string.Empty;

    public string Reason { get; set; } =
        string.Empty;

    public string ReferenceType { get; set; } =
        "Manual";

    public string? ReferenceId { get; set; }

    public decimal UnitCost { get; set; }

    public decimal TotalCost { get; set; }

    public DateTime MovementDate { get; set; } =
        DateTime.UtcNow;
}