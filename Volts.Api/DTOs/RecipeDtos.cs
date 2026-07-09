namespace Volts.Api.DTOs;

public class RecipeCreateDto
{
    public string ProductId { get; set; } = string.Empty;
    public List<RecipeDetailDto> Details { get; set; } = new();
}

public class RecipeUpdateDto : RecipeCreateDto
{
    public bool IsActive { get; set; } = true;
}

public class RecipeDetailDto
{
    public string RawMaterialId { get; set; } = string.Empty;
    public decimal QuantityRequired { get; set; }
}