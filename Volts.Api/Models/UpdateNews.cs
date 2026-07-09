using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class UpdateNews : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Platform { get; set; } = "Android";
    public DateTime PublishDate { get; set; } = DateTime.UtcNow;
    public bool IsPublished { get; set; } = true;
}