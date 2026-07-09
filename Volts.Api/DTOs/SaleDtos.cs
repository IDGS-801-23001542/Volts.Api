namespace Volts.Api.DTOs;

public class SaleCreateDto
{
    public string CustomerId { get; set; } = string.Empty;
    public List<SaleDetailDto> Details { get; set; } = new();
}

public class SaleDetailDto
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}