namespace Volts.Api.DTOs;

public class SupplierCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

public class SupplierUpdateDto : SupplierCreateDto
{
    public bool IsActive { get; set; } = true;
}