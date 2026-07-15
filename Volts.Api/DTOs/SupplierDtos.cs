namespace Volts.Api.DTOs;

public class SupplierAddressDto
{
    public string Street { get; set; } =
        string.Empty;

    public string ExteriorNumber { get; set; } =
        string.Empty;

    public string? InteriorNumber { get; set; }

    public string Neighborhood { get; set; } =
        string.Empty;

    public string PostalCode { get; set; } =
        string.Empty;

    public string City { get; set; } =
        string.Empty;

    public string State { get; set; } =
        string.Empty;

    public string Country { get; set; } =
        "México";

    public string? References { get; set; }
}

public class SupplierCreateDto
{
    public string Code { get; set; } =
        string.Empty;

    public string Name { get; set; } =
        string.Empty;

    public string LegalName { get; set; } =
        string.Empty;

    public string TaxId { get; set; } =
        string.Empty;

    public string ContactName { get; set; } =
        string.Empty;

    public string Email { get; set; } =
        string.Empty;

    public string? Phone { get; set; }

    public SupplierAddressDto Address { get; set; } =
        new();

    public string SupplierType { get; set; } =
        "General";

    public List<string> MaterialCategories
    {
        get;
        set;
    } = new();

    public int LeadTimeDays { get; set; }

    public string PaymentTerms { get; set; } =
        string.Empty;

    public string Notes { get; set; } =
        string.Empty;
}

public class SupplierUpdateDto :
    SupplierCreateDto
{
    public bool IsActive { get; set; } = true;
}