namespace Volts.Api.DTOs;

public class RawMaterialCreateDto
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
     * Ya no se recibe un texto libre como "Pieza",
     * "PZA", "Unidad" o "Kg".
     *
     * Se recibe el Id de una unidad existente y activa
     * del catálogo UnitsOfMeasure.
     */
    public string UnitOfMeasureId { get; set; } =
        string.Empty;

    public decimal CurrentStock { get; set; }

    public decimal MinimumStock { get; set; }

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
}

public class RawMaterialUpdateDto :
    RawMaterialCreateDto
{
    public bool IsActive { get; set; } = true;
}

/*
 * Compatibilidad temporal con los endpoints antiguos
 * add-stock y remove-stock.
 */
public class RawMaterialStockUpdateDto
{
    public decimal Quantity { get; set; }
}

/*
 * Movimiento administrativo manual.
 *
 * MovementType solamente permite:
 * Entry
 * Exit
 *
 * Se eliminó Adjustment porque anteriormente cualquier
 * Adjustment se comportaba como una entrada.
 */
public class RawMaterialStockAdjustmentDto
{
    public string MovementType { get; set; } =
        string.Empty;

    public decimal Quantity { get; set; }

    public string Reason { get; set; } =
        string.Empty;

    public decimal? UnitCost { get; set; }

    public string ReferenceType { get; set; } =
        "Manual";

    public string? ReferenceId { get; set; }
}