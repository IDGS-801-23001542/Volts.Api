using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Quote : BaseEntity
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? InstitutionName { get; set; }

    public string PlanName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Shipping { get; set; }

    public decimal Total { get; set; }

    public string? Notes { get; set; }

    public string Status { get; set; } = "Pending";
}