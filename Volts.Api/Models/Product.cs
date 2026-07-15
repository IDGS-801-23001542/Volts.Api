using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Species { get; set; } = "Perro";
    public string Breed { get; set; } = string.Empty;
    public string CommercialStatus { get; set; } = "ComingSoon";
    public bool CanBePurchased { get; set; }
    public bool CanBeProduced { get; set; } = true;
    public string? ImageUrl { get; set; }

    public int PhysicalStock { get; set; }
    public int ReservedStock { get; set; }
    public int MinimumFinishedStock { get; set; }

    [BsonIgnore]
    public int AvailableStock => PhysicalStock - ReservedStock;

    // Compatibilidad temporal con código antiguo. No usar en Builders<T>.Update.
    [BsonIgnore]
    public int FinishedStock
    {
        get => PhysicalStock;
        set => PhysicalStock = value;
    }

    public bool IsActive { get; set; } = true;
}
