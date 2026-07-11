namespace Volts.Api.DTOs;

public class CategoryCreateDto
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public class CategoryUpdateDto : CategoryCreateDto
{
    public bool IsActive { get; set; } = true;
}