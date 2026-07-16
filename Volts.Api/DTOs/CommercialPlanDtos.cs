namespace Volts.Api.DTOs;

public class CommercialPlanCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int WarrantyMonths { get; set; }
    public string SupportLevel { get; set; } = "Standard";
    public bool IncludesTraining { get; set; }
    public bool IncludesDocumentation { get; set; } = true;
    public bool IncludesUpdates { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public class CommercialPlanUpdateDto : CommercialPlanCreateDto
{
    public bool IsActive { get; set; } = true;
}
