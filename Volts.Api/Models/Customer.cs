using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Customer : BaseEntity
{
    public string CustomerType { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? InstitutionName { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
}