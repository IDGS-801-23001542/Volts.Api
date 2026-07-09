namespace Volts.Api.DTOs;

public class ProductCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
}

public class ProductUpdateDto : ProductCreateDto
{
    public bool IsActive { get; set; } = true;
}