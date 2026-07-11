using MongoDB.Bson.Serialization.Attributes;

namespace Volts.Api.Models;

[BsonIgnoreExtraElements]
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    /*
     * Relación lógica con Categories.
     * MongoDB no utiliza una llave foránea real,
     * pero guardamos el identificador y el nombre.
     */
    public string CategoryId { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    /*
     * Se conserva Category temporalmente para evitar
     * romper partes antiguas del sitio público.
     */
    public string Category { get; set; } = string.Empty;

    /*
     * Información comercial y visual.
     */
    public string Species { get; set; } = "Perro";

    public string Breed { get; set; } = string.Empty;

    public string CommercialStatus { get; set; } = "ComingSoon";

    public bool CanBePurchased { get; set; } = false;

    public bool CanBeProduced { get; set; } = true;

    public string? ImageUrl { get; set; }

    /*
     * Inventario de producto terminado.
     * Este stock aumentará desde Producción.
     */
    public int FinishedStock { get; set; } = 0;

    public int MinimumFinishedStock { get; set; } = 0;

    public bool IsActive { get; set; } = true;
}