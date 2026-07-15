namespace Volts.Api.DTOs;

public class PersonNameDto
{
    public string FirstNames { get; set; } = string.Empty;
    public string PaternalLastName { get; set; } = string.Empty;
    public string? MaternalLastName { get; set; }
}

public class AddressDto
{
    public string Street { get; set; } = string.Empty;
    public string ExteriorNumber { get; set; } = string.Empty;
    public string? InteriorNumber { get; set; }
    public string Neighborhood { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = "México";
    public string? References { get; set; }
}

public class InstitutionResponsibleDto
{
    public PersonNameDto Name { get; set; } = new();
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Position { get; set; }
}

public class EntityStatusUpdateDto
{
    public bool IsActive { get; set; }
}
