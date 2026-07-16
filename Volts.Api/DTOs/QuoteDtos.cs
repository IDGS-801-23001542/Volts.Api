namespace Volts.Api.DTOs;

public class QuoteCreateDto
{
    public string RecipientType { get; set; } = "Customer";
    public string? CustomerId { get; set; }
    public string? InstitutionId { get; set; }

    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }

    public string CommercialPackageId { get; set; } = string.Empty;
    public int PackageQuantity { get; set; } = 1;
    public string? Notes { get; set; }
}

public class QuoteBackofficeCreateDto : QuoteCreateDto
{
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public int ValidityDays { get; set; } = 15;
    public string? Conditions { get; set; }
}

public class QuoteStatusUpdateDto
{
    public string Status { get; set; } = string.Empty;
}

public class QuotePricingUpdateDto
{
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public int ValidityDays { get; set; } = 15;
    public string? Conditions { get; set; }
}
