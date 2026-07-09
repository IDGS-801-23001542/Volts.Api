namespace Volts.Api.DTOs;

public class OrderCreateDto
{
    public string CustomerId { get; set; } = string.Empty;
    public List<OrderDetailDto> Details { get; set; } = new();
}

public class OrderDetailDto
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class OrderStatusUpdateDto
{
    public string Status { get; set; } = string.Empty;
}