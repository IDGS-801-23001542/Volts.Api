using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class SupportTicket : BaseEntity
{
    public string CustomerId { get; set; } =
        string.Empty;

    public string CustomerName { get; set; } =
        string.Empty;

    public string Email { get; set; } =
        string.Empty;

    public string Subject { get; set; } =
        string.Empty;

    public string Description { get; set; } =
        string.Empty;

    public string Priority { get; set; } =
        "Medium";

    public string Status { get; set; } =
        "Open";
}