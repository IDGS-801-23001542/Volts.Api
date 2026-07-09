namespace Volts.Api.DTOs;

public class PurchaseCreateDto
{
    public string SupplierId { get; set; } = string.Empty;
    public List<PurchaseDetailDto> Details { get; set; } = new();
}

public class PurchaseDetailDto
{
    public string RawMaterialId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}