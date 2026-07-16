namespace Volts.Api.DTOs;

public class PurchaseCreateDto
{
    public string SupplierId { get; set; } =
        string.Empty;

    public string? InvoiceNumber { get; set; }

    public DateTime? PurchaseDate { get; set; }

    public decimal Tax { get; set; }

    public decimal ShippingCost { get; set; }

    public string Notes { get; set; } =
        string.Empty;

    public List<PurchaseDetailDto> Details
    {
        get;
        set;
    } = new();
}

public class PurchaseDetailDto
{
    public string RawMaterialId { get; set; } =
        string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }
}

public class PurchaseSummaryDto
{
    public int TotalPurchases { get; set; }

    public int PurchasesThisMonth { get; set; }

    public decimal TotalInvested { get; set; }

    public decimal InvestedThisMonth
    {
        get;
        set;
    }

    public decimal AveragePurchaseValue
    {
        get;
        set;
    }

    public int SuppliersUsed { get; set; }
}