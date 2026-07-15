using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class License : BaseEntity
{
    public string LicenseCode { get; set; } = string.Empty;

    public string SaleId { get; set; } = string.Empty;
    public string SaleFolio { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string OrderFolio { get; set; } = string.Empty;
    public string SaleDetailId { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    public string CommercialPlanId { get; set; } = string.Empty;
    public string CommercialPlanName { get; set; } = string.Empty;
    public string CommercialPackageId { get; set; } = string.Empty;
    public string CommercialPackageName { get; set; } = string.Empty;

    public string RecipientType { get; set; } = "Customer";
    public string? CustomerId { get; set; }
    public string? InstitutionId { get; set; }
    public string RecipientName { get; set; } = string.Empty;

    public string Status { get; set; } = "Available";
    public DateTime WarrantyStartDate { get; set; }
    public DateTime WarrantyEndDate { get; set; }
    public DateTime? ActivationDate { get; set; }
    public DateTime? ExpirationDate { get; set; }

    public string? AssignedToName { get; set; }
    public string? AssignedToEmail { get; set; }
    public string? DeviceSerialNumber { get; set; }
}
