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

    public CategoriesController(MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // GET: api/Categories
    // Sitio público: devuelve únicamente categorías activas.
    // =========================================================
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic()
    {
        var categories = await _db.Categories
            .Find(category =>
                !category.IsDeleted &&
                category.IsActive)
            .SortBy(category => category.Name)
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
    // Admin y Employee pueden consultar activas e inactivas.
    // =========================================================
    [HttpGet("backoffice")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetBackoffice()
    {
        var categories = await _db.Categories
            .Find(category => !category.IsDeleted)
            .SortBy(category => category.Name)
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
    public async Task<IActionResult> GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<Category>.Fail(
                    "El identificador de la categoría es obligatorio"
                )
            );
        }

        var category = await _db.Categories
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
        var validationError = ValidateCategory(
            dto.Name,
            dto.Description
        );

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<Category>.Fail(validationError)
            );
        }

        var normalizedName = dto.Name.Trim();
        var normalizedDescription = dto.Description.Trim();

        var exists = await _db.Categories
            .Find(category =>
                category.Name.ToLower() ==
                normalizedName.ToLower() &&
                !category.IsDeleted)
            .AnyAsync();

        if (exists)
        {
            return BadRequest(
                ApiResponse<Category>.Fail(
                    "Ya existe una categoría con ese nombre"
                )
            );
        }

        var category = new Category
        {
            Name = normalizedName,
            Description = normalizedDescription,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };

        await _db.Categories.InsertOneAsync(category);

        return CreatedAtAction(
            nameof(GetById),
            new { id = category.Id },
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
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<Category>.Fail(
                    "El identificador de la categoría es obligatorio"
                )
            );
        }

        var validationError = ValidateCategory(
            dto.Name,
            dto.Description
        );

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<Category>.Fail(validationError)
            );
        }

        var category = await _db.Categories
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

        var normalizedName = dto.Name.Trim();
        var normalizedDescription = dto.Description.Trim();

        var duplicateExists = await _db.Categories
            .Find(item =>
                item.Id != id &&
                item.Name.ToLower() ==
                normalizedName.ToLower() &&
                !item.IsDeleted)
            .AnyAsync();

        if (duplicateExists)
        {
            return BadRequest(
                ApiResponse<Category>.Fail(
                    "Ya existe otra categoría con ese nombre"
                )
            );
        }

        category.Name = normalizedName;
        category.Description = normalizedDescription;
        category.IsActive = dto.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        var result = await _db.Categories.ReplaceOneAsync(
            item =>
                item.Id == id &&
                !item.IsDeleted,
            category
        );

        if (result.MatchedCount == 0)
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
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "El identificador de la categoría es obligatorio"
                )
            );
        }

        var update = Builders<Category>.Update
            .Set(category => category.IsActive, isActive)
            .Set(category => category.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Categories.UpdateOneAsync(
            category =>
                category.Id == id &&
                !category.IsDeleted,
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

        var message = isActive
            ? "Categoría activada correctamente"
            : "Categoría desactivada correctamente";

        return Ok(ApiResponse<string>.Ok(message));
    }

    // =========================================================
    // DELETE: api/Categories/{id}
    // Eliminación lógica. Solo Admin.
    // =========================================================
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "El identificador de la categoría es obligatorio"
                )
            );
        }

        var update = Builders<Category>.Update
            .Set(category => category.IsDeleted, true)
            .Set(category => category.IsActive, false)
            .Set(category => category.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Categories.UpdateOneAsync(
            category =>
                category.Id == id &&
                !category.IsDeleted,
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
        string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "El nombre de la categoría es obligatorio";

        if (name.Trim().Length < 3)
            return "El nombre debe tener al menos 3 caracteres";

        if (name.Trim().Length > 80)
            return "El nombre no puede superar los 80 caracteres";

        if (description?.Trim().Length > 300)
            return "La descripción no puede superar los 300 caracteres";

        return null;
    }
}