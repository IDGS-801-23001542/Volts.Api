using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Order : BaseEntity
{
    public string Folio { get; set; } = string.Empty;
    public string QuoteId { get; set; } = string.Empty;
    public string QuoteFolio { get; set; } = string.Empty;

    public string RecipientType { get; set; } = "Customer";
    public string? CustomerId { get; set; }
    public string? InstitutionId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }

    public string CommercialPlanId { get; set; } = string.Empty;
    public string CommercialPlanName { get; set; } = string.Empty;
    public string CommercialPackageId { get; set; } = string.Empty;
    public string CommercialPackageName { get; set; } = string.Empty;

    public string Status { get; set; } = "PendingConfirmation";

    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Shipping { get; set; }
    public decimal Total { get; set; }

    public List<OrderDetail> Details { get; set; } = new();
    public List<string> ProductionOrderIds { get; set; } = new();

    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ReadyForSaleAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}

public class OrderDetail
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int PendingQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}
