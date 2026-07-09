namespace Volts.Api.DTOs;

public class InstitutionCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string InstitutionType { get; set; } = string.Empty;
}

public class InstitutionUpdateDto : InstitutionCreateDto
{
    public bool IsActive { get; set; } = true;
}