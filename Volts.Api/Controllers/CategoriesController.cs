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
public class CategoriesController : ControllerBase
{
    private readonly MongoDbService _db;

    public CategoriesController(
        MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // GET: api/Categories
    // Público: solamente activas.
    // =========================================================
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic()
    {
        var categories =
            await _db.Categories
                .Find(item =>
                    !item.IsDeleted &&
                    item.IsActive)
                .SortBy(item => item.Name)
                .ToListAsync();

        return Ok(
            ApiResponse<List<Category>>.Ok(
                categories,
                "Categorías activas obtenidas correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/Categories/backoffice
    // =========================================================
    [HttpGet("backoffice")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetBackoffice()
    {
        var categories =
            await _db.Categories
                .Find(item =>
                    !item.IsDeleted)
                .SortBy(item => item.Name)
                .ToListAsync();

        return Ok(
            ApiResponse<List<Category>>.Ok(
                categories,
                "Categorías del backoffice obtenidas correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/Categories/{id}
    // =========================================================
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetById(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<Category>.Fail(
                    "El identificador de la categoría es obligatorio"
                )
            );
        }

        var category =
            await _db.Categories
                .Find(item =>
                    item.Id == id &&
                    !item.IsDeleted)
                .FirstOrDefaultAsync();

        if (category == null)
        {
            return NotFound(
                ApiResponse<Category>.Fail(
                    "Categoría no encontrada"
                )
            );
        }

        return Ok(
            ApiResponse<Category>.Ok(
                category,
                "Categoría obtenida correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/Categories
    // Solo Admin.
    // =========================================================
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        [FromBody] CategoryCreateDto dto)
    {
        var validationError =
            ValidateCategory(
                dto.Name,
                dto.Description
            );

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<Category>.Fail(
                    validationError
                )
            );
        }

        var normalizedName =
            dto.Name.Trim();

        var duplicate =
            await _db.Categories
                .Find(item =>
                    item.Name.ToLower() ==
                        normalizedName.ToLower() &&
                    !item.IsDeleted)
                .AnyAsync();

        if (duplicate)
        {
            return BadRequest(
                ApiResponse<Category>.Fail(
                    "Ya existe una categoría con ese nombre"
                )
            );
        }

        var category = new Category
        {
            Name =
                normalizedName,

            Description =
                dto.Description?.Trim() ??
                string.Empty,

            IsActive =
                true,

            IsDeleted =
                false,

            CreatedAt =
                DateTime.UtcNow
        };

        await _db.Categories
            .InsertOneAsync(category);

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                id = category.Id
            },
            ApiResponse<Category>.Ok(
                category,
                "Categoría creada correctamente"
            )
        );
    }

    // =========================================================
    // PUT: api/Categories/{id}
    // Solo Admin.
    // =========================================================
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] CategoryUpdateDto dto)
    {
        var validationError =
            ValidateCategory(
                dto.Name,
                dto.Description
            );

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<Category>.Fail(
                    validationError
                )
            );
        }

        var category =
            await _db.Categories
                .Find(item =>
                    item.Id == id &&
                    !item.IsDeleted)
                .FirstOrDefaultAsync();

        if (category == null)
        {
            return NotFound(
                ApiResponse<Category>.Fail(
                    "Categoría no encontrada"
                )
            );
        }

        if (!dto.IsActive)
        {
            var hasActiveProducts =
                await _db.Products
                    .Find(product =>
                        product.CategoryId == id &&
                        !product.IsDeleted &&
                        product.IsActive)
                    .AnyAsync();

            if (hasActiveProducts)
            {
                return BadRequest(
                    ApiResponse<Category>.Fail(
                        "No se puede desactivar la categoría porque tiene productos activos"
                    )
                );
            }
        }

        var normalizedName =
            dto.Name.Trim();

        var duplicate =
            await _db.Categories
                .Find(item =>
                    item.Id != id &&
                    item.Name.ToLower() ==
                        normalizedName.ToLower() &&
                    !item.IsDeleted)
                .AnyAsync();

        if (duplicate)
        {
            return BadRequest(
                ApiResponse<Category>.Fail(
                    "Ya existe otra categoría con ese nombre"
                )
            );
        }

        var previousName =
            category.Name;

        category.Name =
            normalizedName;

        category.Description =
            dto.Description?.Trim() ??
            string.Empty;

        category.IsActive =
            dto.IsActive;

        category.UpdatedAt =
            DateTime.UtcNow;

        using var session =
            await _db.StartSessionAsync();

        try
        {
            session.StartTransaction();

            var categoryResult =
                await _db.Categories
                    .ReplaceOneAsync(
                        session,
                        item =>
                            item.Id == id &&
                            !item.IsDeleted,
                        category
                    );

            if (categoryResult.MatchedCount == 0)
            {
                await session
                    .AbortTransactionAsync();

                return NotFound(
                    ApiResponse<Category>.Fail(
                        "Categoría no encontrada"
                    )
                );
            }

            if (!string.Equals(
                    previousName,
                    category.Name,
                    StringComparison.Ordinal))
            {
                var productUpdate =
                    Builders<Product>.Update
                        .Set(
                            product =>
                                product.CategoryName,
                            category.Name
                        )
                        .Set(
                            product =>
                                product.Category,
                            category.Name
                        )
                        .Set(
                            product =>
                                product.UpdatedAt,
                            DateTime.UtcNow
                        );

                await _db.Products
                    .UpdateManyAsync(
                        session,
                        product =>
                            product.CategoryId == id &&
                            !product.IsDeleted,
                        productUpdate
                    );
            }

            await session
                .CommitTransactionAsync();
        }
        catch
        {
            await session
                .AbortTransactionAsync();

            throw;
        }

        return Ok(
            ApiResponse<Category>.Ok(
                category,
                "Categoría actualizada correctamente"
            )
        );
    }

    // =========================================================
    // PATCH: api/Categories/{id}/status
    // Solo Admin.
    // =========================================================
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(
        string id,
        [FromQuery] bool isActive)
    {
        var category =
            await _db.Categories
                .Find(item =>
                    item.Id == id &&
                    !item.IsDeleted)
                .FirstOrDefaultAsync();

        if (category == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Categoría no encontrada"
                )
            );
        }

        if (!isActive)
        {
            var hasActiveProducts =
                await _db.Products
                    .Find(product =>
                        product.CategoryId == id &&
                        !product.IsDeleted &&
                        product.IsActive)
                    .AnyAsync();

            if (hasActiveProducts)
            {
                return BadRequest(
                    ApiResponse<string>.Fail(
                        "No se puede desactivar la categoría porque tiene productos activos"
                    )
                );
            }
        }

        var update =
            Builders<Category>.Update
                .Set(
                    item =>
                        item.IsActive,
                    isActive
                )
                .Set(
                    item =>
                        item.UpdatedAt,
                    DateTime.UtcNow
                );

        var result =
            await _db.Categories
                .UpdateOneAsync(
                    item =>
                        item.Id == id &&
                        !item.IsDeleted,
                    update
                );

        if (result.MatchedCount == 0)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Categoría no encontrada"
                )
            );
        }

        return Ok(
            ApiResponse<string>.Ok(
                isActive
                    ? "Categoría activada correctamente"
                    : "Categoría desactivada correctamente"
            )
        );
    }

    // =========================================================
    // DELETE: api/Categories/{id}
    // Solo Admin.
    // =========================================================
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(
        string id)
    {
        var category =
            await _db.Categories
                .Find(item =>
                    item.Id == id &&
                    !item.IsDeleted)
                .FirstOrDefaultAsync();

        if (category == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Categoría no encontrada"
                )
            );
        }

        var hasProducts =
            await _db.Products
                .Find(product =>
                    product.CategoryId == id &&
                    !product.IsDeleted)
                .AnyAsync();

        if (hasProducts)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "No se puede eliminar una categoría con productos relacionados. Puedes desactivarla después de desactivar sus productos."
                )
            );
        }

        var update =
            Builders<Category>.Update
                .Set(
                    item =>
                        item.IsDeleted,
                    true
                )
                .Set(
                    item =>
                        item.IsActive,
                    false
                )
                .Set(
                    item =>
                        item.UpdatedAt,
                    DateTime.UtcNow
                );

        var result =
            await _db.Categories
                .UpdateOneAsync(
                    item =>
                        item.Id == id &&
                        !item.IsDeleted,
                    update
                );

        if (result.MatchedCount == 0)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Categoría no encontrada"
                )
            );
        }

        return Ok(
            ApiResponse<string>.Ok(
                "Categoría eliminada correctamente"
            )
        );
    }

    private static string? ValidateCategory(
        string name,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return
                "El nombre de la categoría es obligatorio";
        }

        if (name.Trim().Length < 3 ||
            name.Trim().Length > 80)
        {
            return
                "El nombre debe tener entre 3 y 80 caracteres";
        }

        if (description?.Trim().Length > 300)
        {
            return
                "La descripción no puede superar los 300 caracteres";
        }

        return null;
    }
}