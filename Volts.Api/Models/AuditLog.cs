using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class AuditLog : BaseEntity
{
    public string? UserId { get; set; }
    public string UserName { get; set; } = "Visitante público";
    public string RoleName { get; set; } = "Public";
    public string ActorType { get; set; } = "Public";

    public string Area { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? EntityFolio { get; set; }

    public string Result { get; set; } = "Successful";
    public string Description { get; set; } = string.Empty;

    public string HttpMethod { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string CorrelationId { get; set; } = string.Empty;

    public string? RequestData { get; set; }
    public string? ResponseData { get; set; }
}
