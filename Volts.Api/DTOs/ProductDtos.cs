namespace Volts.Api.DTOs;

public class ProductCreateDto
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string CategoryId { get; set; } = string.Empty;

    public string Species { get; set; } = "Perro";

    public string Breed { get; set; } = string.Empty;

    public string CommercialStatus { get; set; } = "ComingSoon";

    public bool CanBePurchased { get; set; } = false;

    public bool CanBeProduced { get; set; } = true;

    public string? ImageUrl { get; set; }

    public int MinimumFinishedStock { get; set; }
}

public class ProductUpdateDto : ProductCreateDto
{
    public bool IsActive { get; set; } = true;
}

public class ProductFinishedStockUpdateDto
{
    public int Quantity { get; set; }

    public string Reason { get; set; } = string.Empty;
}