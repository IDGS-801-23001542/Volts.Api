using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Documentation : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public bool IsPublic { get; set; } = false;
    public bool IsActive { get; set; } = true;
}