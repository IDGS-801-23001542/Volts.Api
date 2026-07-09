namespace Volts.Api.DTOs;

public class WasteCreateDto
{
    public string RawMaterialId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
}