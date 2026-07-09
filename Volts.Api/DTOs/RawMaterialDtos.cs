namespace Volts.Api.DTOs;

public class RawMaterialCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal MaximumStock { get; set; }
    public decimal AverageCost { get; set; }
}

public class RawMaterialUpdateDto : RawMaterialCreateDto
{
    public bool IsActive { get; set; } = true;
}

public class RawMaterialStockUpdateDto
{
    public decimal Quantity { get; set; }
}