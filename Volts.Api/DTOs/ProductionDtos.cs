namespace Volts.Api.DTOs;

public class ProductionCreateDto
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class ProductionStatusUpdateDto
{
    public string Status { get; set; } = string.Empty;
}