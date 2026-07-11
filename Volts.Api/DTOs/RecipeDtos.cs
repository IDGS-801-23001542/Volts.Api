namespace Volts.Api.DTOs;

public class RecipeCreateDto
{
    public string ProductId { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public string Notes { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public List<RecipeDetailDto> Details { get; set; } = new();
}

public class RecipeUpdateDto : RecipeCreateDto
{
}

public class RecipeDetailDto
{
    public string RawMaterialId { get; set; } = string.Empty;

    public decimal QuantityRequired { get; set; }

    public decimal WastePercentage { get; set; }

    public bool AcceptsRecoveredWaste { get; set; }
}