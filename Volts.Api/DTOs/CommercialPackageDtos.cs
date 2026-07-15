namespace Volts.Api.DTOs;

public class CommercialPackageItemDto
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class CommercialPackageCreateDto
{
    public string CommercialPlanId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public List<CommercialPackageItemDto> Items { get; set; } = new();
    public int DisplayOrder { get; set; }
}

public class CommercialPackageUpdateDto : CommercialPackageCreateDto
{
    public bool IsActive { get; set; } = true;
}
