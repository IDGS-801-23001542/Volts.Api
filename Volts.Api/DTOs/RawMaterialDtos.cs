namespace Volts.Api.DTOs;

public class RawMaterialCreateDto
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal CurrentStock { get; set; }

    public decimal MinimumStock { get; set; }

    public decimal MaximumStock { get; set; }

    public decimal AverageCost { get; set; }

    public decimal LastPurchaseCost { get; set; }

    public bool IsRecycled { get; set; }

    public bool IsReusable { get; set; }

    public bool RequiresPurchase { get; set; } = true;

    public string StorageLocation { get; set; } = string.Empty;

    public string? PreferredSupplierId { get; set; }

    public string? PreferredSupplierName { get; set; }
}

public class RawMaterialUpdateDto : RawMaterialCreateDto
{
    public bool IsActive { get; set; } = true;
}

public class RawMaterialStockUpdateDto
{
    public decimal Quantity { get; set; }
}

public class RawMaterialStockAdjustmentDto
{
    public string MovementType { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string Reason { get; set; } = string.Empty;

    public decimal? UnitCost { get; set; }

    public string ReferenceType { get; set; } = "Manual";

    public string? ReferenceId { get; set; }
}