namespace Volts.Api.DTOs;

public class CustomerCreateDto : PortalAccountRequestDto
{
    public PersonNameDto Name { get; set; } = new();
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public AddressDto? Address { get; set; }
}

public class CustomerUpdateDto
{
    public PersonNameDto Name { get; set; } = new();
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public AddressDto? Address { get; set; }
    public bool IsActive { get; set; } = true;
}
