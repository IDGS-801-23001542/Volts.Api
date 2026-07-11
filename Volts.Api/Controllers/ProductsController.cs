using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Volts.Api.DTOs;
using Volts.Api.Models;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private static readonly string[] AllowedCommercialStatuses =
    {
        "Available",
        "ComingSoon",
        "Unavailable",
        "Discontinued"
    };

    private readonly MongoDbService _db;

    public ProductsController(MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // GET: api/Products
    // Catálogo público.
    // Incluye productos activos aunque estén "Próximamente".
    // =========================================================
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic()
    {
        var products = await _db.Products
            .Find(product =>
                !product.IsDeleted &&
                product.IsActive)
            .SortBy(product => product.Name)
            .ToListAsync();

        return Ok(
            ApiResponse<List<Product>>.Ok(
                products,
                "Productos obtenidos correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/Products/backoffice
    // Admin y Employee ven activos e inactivos.
    // =========================================================
    [HttpGet("backoffice")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetBackoffice()
    {
        var products = await _db.Products
            .Find(product => !product.IsDeleted)
            .SortBy(product => product.Name)
            .ToListAsync();

        return Ok(
            ApiResponse<List<Product>>.Ok(
                products,
                "Productos del backoffice obtenidos correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/Products/purchasable
    // Solo productos que realmente pueden adquirirse.
    // =========================================================
    [HttpGet("purchasable")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPurchasable()
    {
        var products = await _db.Products
            .Find(product =>
                !product.IsDeleted &&
                product.IsActive &&
                product.CanBePurchased &&
                product.CommercialStatus == "Available")
            .SortBy(product => product.Name)
            .ToListAsync();

        return Ok(
            ApiResponse<List<Product>>.Ok(
                products,
                "Productos disponibles para compra obtenidos correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/Products/{id}
    // =========================================================
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(string id)
    {
        var product = await _db.Products
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return NotFound(
                ApiResponse<Product>.Fail(
                    "Producto no encontrado"
                )
            );
        }

        return Ok(
            ApiResponse<Product>.Ok(
                product,
                "Producto obtenido correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/Products
    // Solo Admin.
    // =========================================================
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        [FromBody] ProductCreateDto dto)
    {
        var validationError = ValidateProduct(dto);

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<Product>.Fail(validationError)
            );
        }

        var category = await _db.Categories
            .Find(item =>
                item.Id == dto.CategoryId &&
                !item.IsDeleted &&
                item.IsActive)
            .FirstOrDefaultAsync();

        if (category == null)
        {
            return BadRequest(
                ApiResponse<Product>.Fail(
                    "La categoría seleccionada no existe o está inactiva"
                )
            );
        }

        var normalizedSlug = NormalizeSlug(dto.Slug);

        var duplicateName = await _db.Products
            .Find(item =>
                item.Name.ToLower() ==
                dto.Name.Trim().ToLower() &&
                !item.IsDeleted)
            .AnyAsync();

        if (duplicateName)
        {
            return BadRequest(
                ApiResponse<Product>.Fail(
                    "Ya existe un producto con ese nombre"
                )
            );
        }

        var duplicateSlug = await _db.Products
            .Find(item =>
                item.Slug.ToLower() ==
                normalizedSlug.ToLower() &&
                !item.IsDeleted)
            .AnyAsync();

        if (duplicateSlug)
        {
            return BadRequest(
                ApiResponse<Product>.Fail(
                    "Ya existe un producto con ese identificador URL"
                )
            );
        }

        var commercialStatus = dto.CommercialStatus.Trim();

        var product = new Product
        {
            Name = dto.Name.Trim(),
            Slug = normalizedSlug,
            Description = dto.Description.Trim(),
            Price = dto.Price,

            CategoryId = category.Id,
            CategoryName = category.Name,

            // Compatibilidad temporal.
            Category = category.Name,

            Species = dto.Species.Trim(),
            Breed = dto.Breed.Trim(),

            CommercialStatus = commercialStatus,

            /*
             * Solo un producto con estado Available puede
             * quedar habilitado para compra.
             */
            CanBePurchased =
                commercialStatus == "Available" &&
                dto.CanBePurchased,

            CanBeProduced = dto.CanBeProduced,

            ImageUrl = NormalizeOptional(dto.ImageUrl),

            FinishedStock = 0,

            MinimumFinishedStock =
                dto.MinimumFinishedStock,

            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };

        await _db.Products.InsertOneAsync(product);

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            ApiResponse<Product>.Ok(
                product,
                "Producto creado correctamente"
            )
        );
    }

    // =========================================================
    // PUT: api/Products/{id}
    // Solo Admin.
    // =========================================================
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] ProductUpdateDto dto)
    {
        var validationError = ValidateProduct(dto);

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<Product>.Fail(validationError)
            );
        }

        var product = await _db.Products
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return NotFound(
                ApiResponse<Product>.Fail(
                    "Producto no encontrado"
                )
            );
        }

        var category = await _db.Categories
            .Find(item =>
                item.Id == dto.CategoryId &&
                !item.IsDeleted &&
                item.IsActive)
            .FirstOrDefaultAsync();

        if (category == null)
        {
            return BadRequest(
                ApiResponse<Product>.Fail(
                    "La categoría seleccionada no existe o está inactiva"
                )
            );
        }

        var normalizedSlug = NormalizeSlug(dto.Slug);

        var duplicateName = await _db.Products
            .Find(item =>
                item.Id != id &&
                item.Name.ToLower() ==
                dto.Name.Trim().ToLower() &&
                !item.IsDeleted)
            .AnyAsync();

        if (duplicateName)
        {
            return BadRequest(
                ApiResponse<Product>.Fail(
                    "Ya existe otro producto con ese nombre"
                )
            );
        }

        var duplicateSlug = await _db.Products
            .Find(item =>
                item.Id != id &&
                item.Slug.ToLower() ==
                normalizedSlug.ToLower() &&
                !item.IsDeleted)
            .AnyAsync();

        if (duplicateSlug)
        {
            return BadRequest(
                ApiResponse<Product>.Fail(
                    "Ya existe otro producto con ese identificador URL"
                )
            );
        }

        var commercialStatus = dto.CommercialStatus.Trim();

        product.Name = dto.Name.Trim();
        product.Slug = normalizedSlug;
        product.Description = dto.Description.Trim();
        product.Price = dto.Price;

        product.CategoryId = category.Id;
        product.CategoryName = category.Name;
        product.Category = category.Name;

        product.Species = dto.Species.Trim();
        product.Breed = dto.Breed.Trim();

        product.CommercialStatus = commercialStatus;

        product.CanBePurchased =
            commercialStatus == "Available" &&
            dto.CanBePurchased;

        product.CanBeProduced = dto.CanBeProduced;

        product.ImageUrl = NormalizeOptional(
            dto.ImageUrl
        );

        product.MinimumFinishedStock =
            dto.MinimumFinishedStock;

        product.IsActive = dto.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.Products.ReplaceOneAsync(
            item =>
                item.Id == id &&
                !item.IsDeleted,
            product
        );

        return Ok(
            ApiResponse<Product>.Ok(
                product,
                "Producto actualizado correctamente"
            )
        );
    }

    // =========================================================
    // PATCH: api/Products/{id}/status
    // Activación o desactivación.
    // =========================================================
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(
        string id,
        [FromQuery] bool isActive)
    {
        var update = Builders<Product>.Update
            .Set(item => item.IsActive, isActive)
            .Set(item => item.UpdatedAt, DateTime.UtcNow);

        /*
         * Si se desactiva, tampoco podrá comprarse.
         */
        if (!isActive)
        {
            update = update.Set(
                item => item.CanBePurchased,
                false
            );
        }

        var result = await _db.Products.UpdateOneAsync(
            item =>
                item.Id == id &&
                !item.IsDeleted,
            update
        );

        if (result.MatchedCount == 0)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Producto no encontrado"
                )
            );
        }

        return Ok(
            ApiResponse<string>.Ok(
                isActive
                    ? "Producto activado correctamente"
                    : "Producto desactivado correctamente"
            )
        );
    }

    // =========================================================
    // PATCH: api/Products/{id}/finished-stock
    // Ajuste administrativo manual.
    // La producción normal utilizará otro flujo.
    // =========================================================
    [HttpPatch("{id}/finished-stock")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdjustFinishedStock(
        string id,
        [FromBody] ProductFinishedStockUpdateDto dto)
    {
        if (dto.Quantity == 0)
        {
            return BadRequest(
                ApiResponse<Product>.Fail(
                    "La cantidad del ajuste no puede ser cero"
                )
            );
        }

        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return BadRequest(
                ApiResponse<Product>.Fail(
                    "Debes indicar el motivo del ajuste"
                )
            );
        }

        var product = await _db.Products
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return NotFound(
                ApiResponse<Product>.Fail(
                    "Producto no encontrado"
                )
            );
        }

        var newStock =
            product.FinishedStock + dto.Quantity;

        if (newStock < 0)
        {
            return BadRequest(
                ApiResponse<Product>.Fail(
                    "El ajuste dejaría el inventario en un valor negativo"
                )
            );
        }

        product.FinishedStock = newStock;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.Products.ReplaceOneAsync(
            item => item.Id == id,
            product
        );

        return Ok(
            ApiResponse<Product>.Ok(
                product,
                "Inventario terminado ajustado correctamente"
            )
        );
    }

    // =========================================================
    // DELETE: api/Products/{id}
    // Eliminación lógica.
    // =========================================================
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var product = await _db.Products
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Producto no encontrado"
                )
            );
        }

        if (product.FinishedStock > 0)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "No se puede eliminar un producto que todavía tiene inventario terminado"
                )
            );
        }

        var update = Builders<Product>.Update
            .Set(item => item.IsDeleted, true)
            .Set(item => item.IsActive, false)
            .Set(item => item.CanBePurchased, false)
            .Set(item => item.UpdatedAt, DateTime.UtcNow);

        await _db.Products.UpdateOneAsync(
            item => item.Id == id,
            update
        );

        return Ok(
            ApiResponse<string>.Ok(
                "Producto eliminado correctamente"
            )
        );
    }

    private static string? ValidateProduct(
        ProductCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return "El nombre del producto es obligatorio";

        if (dto.Name.Trim().Length < 3)
            return "El nombre debe tener al menos 3 caracteres";

        if (dto.Name.Trim().Length > 120)
            return "El nombre no puede superar los 120 caracteres";

        if (string.IsNullOrWhiteSpace(dto.Slug))
            return "El identificador URL es obligatorio";

        if (dto.Slug.Trim().Length > 140)
            return "El identificador URL no puede superar los 140 caracteres";

        if (string.IsNullOrWhiteSpace(dto.Description))
            return "La descripción es obligatoria";

        if (dto.Description.Trim().Length > 800)
            return "La descripción no puede superar los 800 caracteres";

        if (dto.Price < 0)
            return "El precio no puede ser negativo";

        if (string.IsNullOrWhiteSpace(dto.CategoryId))
            return "Debes seleccionar una categoría";

        if (string.IsNullOrWhiteSpace(dto.Species))
            return "La especie es obligatoria";

        if (string.IsNullOrWhiteSpace(dto.Breed))
            return "La raza o variante es obligatoria";

        if (!AllowedCommercialStatuses.Contains(
            dto.CommercialStatus))
        {
            return "El estado comercial no es válido";
        }

        if (dto.MinimumFinishedStock < 0)
            return "El stock mínimo no puede ser negativo";

        return null;
    }

    private static string NormalizeSlug(
        string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "-");
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}