using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class License : BaseEntity
{
    public string LicenseCode { get; set; } = string.Empty;

    public string CustomerId { get; set; } = string.Empty;

    public string? InstitutionId { get; set; }

    public string PlanName { get; set; } = string.Empty;

    public string Status { get; set; } = "Available";

    public DateTime? ActivationDate { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public string? AssignedToName { get; set; }

    public string? AssignedToEmail { get; set; }

    public string? DeviceSerialNumber { get; set; }
}