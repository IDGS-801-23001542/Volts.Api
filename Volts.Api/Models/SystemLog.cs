using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class SystemLog : BaseEntity
{
    public string Level { get; set; } = "Error";
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public string? ExceptionType { get; set; }
    public string? StackTrace { get; set; }

    public string HttpMethod { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }

    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? RoleName { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string CorrelationId { get; set; } = string.Empty;

    public string? AdditionalData { get; set; }
}
