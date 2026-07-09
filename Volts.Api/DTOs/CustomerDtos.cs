namespace Volts.Api.DTOs;

public class CustomerCreateDto
{
    public string CustomerType { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? InstitutionName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

public class CustomerUpdateDto : CustomerCreateDto
{
    public bool IsActive { get; set; } = true;
}