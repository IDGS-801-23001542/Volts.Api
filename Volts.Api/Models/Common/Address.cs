namespace Volts.Api.Models.Common;

public class Address
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
