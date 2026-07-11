using System.Security.Claims;
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
[Authorize(Roles = "Admin,Employee")]
public class RecipesController : ControllerBase
{
    private readonly MongoDbService _db;

    public RecipesController(MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // GET: api/Recipes
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var recipes = await _db.Recipes
            .Find(recipe => !recipe.IsDeleted)
            .SortBy(recipe => recipe.ProductName)
            .ThenByDescending(recipe => recipe.Version)
            .ToListAsync();

        return Ok(
            ApiResponse<List<Recipe>>.Ok(
                recipes,
                "Recetas obtenidas correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/Recipes/{id}
    // =========================================================
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var recipe = await _db.Recipes
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (recipe == null)
        {
            return NotFound(
                ApiResponse<Recipe>.Fail(
                    "Receta no encontrada"
                )
            );
        }

        return Ok(
            ApiResponse<Recipe>.Ok(
                recipe,
                "Receta obtenida correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/Recipes/product/{productId}
    // Devuelve la receta activa del producto.
    // =========================================================
    [HttpGet("product/{productId}")]
    public async Task<IActionResult> GetByProduct(
        string productId)
    {
        var recipe = await _db.Recipes
            .Find(item =>
                item.ProductId == productId &&
                !item.IsDeleted &&
                item.IsActive)
            .SortByDescending(item => item.Version)
            .FirstOrDefaultAsync();

        if (recipe == null)
        {
            return NotFound(
                ApiResponse<Recipe>.Fail(
                    "El producto no tiene una receta activa"
                )
            );
        }

        return Ok(
            ApiResponse<Recipe>.Ok(
                recipe,
                "Receta activa obtenida correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/Recipes
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] RecipeCreateDto dto)
    {
        var validationError = ValidateRecipe(dto);

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<Recipe>.Fail(
                    validationError
                )
            );
        }

        var product = await _db.Products
            .Find(item =>
                item.Id == dto.ProductId &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return BadRequest(
                ApiResponse<Recipe>.Fail(
                    "Producto no encontrado"
                )
            );
        }

        var versionExists = await _db.Recipes
            .Find(item =>
                item.ProductId == dto.ProductId &&
                item.Version == dto.Version &&
                !item.IsDeleted)
            .AnyAsync();

        if (versionExists)
        {
            return BadRequest(
                ApiResponse<Recipe>.Fail(
                    $"El producto ya tiene una receta versión {dto.Version}"
                )
            );
        }

        var repeatedMaterial = dto.Details
            .GroupBy(item => item.RawMaterialId)
            .FirstOrDefault(group => group.Count() > 1);

        if (repeatedMaterial != null)
        {
            return BadRequest(
                ApiResponse<Recipe>.Fail(
                    "Una materia prima no puede repetirse en la receta"
                )
            );
        }

        var detailsResult =
            await BuildRecipeDetailsAsync(dto.Details);

        if (!detailsResult.Success)
        {
            return BadRequest(
                ApiResponse<Recipe>.Fail(
                    detailsResult.ErrorMessage!
                )
            );
        }

        if (dto.IsActive)
        {
            await DeactivateProductRecipesAsync(
                dto.ProductId
            );
        }

        var code =
            $"REC-{product.Slug.ToUpperInvariant()}-V{dto.Version}";

        var recipe = new Recipe
        {
            Code = code,
            ProductId = product.Id,
            ProductName = product.Name,
            Version = dto.Version,
            Notes = dto.Notes.Trim(),
            Details = detailsResult.Details,
            EstimatedUnitCost =
                detailsResult.Details.Sum(
                    item => item.EstimatedSubtotal
                ),
            IsActive = dto.IsActive,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = GetCurrentUserId()
        };

        await _db.Recipes.InsertOneAsync(recipe);

        return CreatedAtAction(
            nameof(GetById),
            new { id = recipe.Id },
            ApiResponse<Recipe>.Ok(
                recipe,
                "Receta creada correctamente"
            )
        );
    }

    // =========================================================
    // PUT: api/Recipes/{id}
    // =========================================================
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] RecipeUpdateDto dto)
    {
        var validationError = ValidateRecipe(dto);

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<Recipe>.Fail(
                    validationError
                )
            );
        }

        var recipe = await _db.Recipes
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (recipe == null)
        {
            return NotFound(
                ApiResponse<Recipe>.Fail(
                    "Receta no encontrada"
                )
            );
        }

        var product = await _db.Products
            .Find(item =>
                item.Id == dto.ProductId &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return BadRequest(
                ApiResponse<Recipe>.Fail(
                    "Producto no encontrado"
                )
            );
        }

        var duplicateVersion = await _db.Recipes
            .Find(item =>
                item.Id != id &&
                item.ProductId == dto.ProductId &&
                item.Version == dto.Version &&
                !item.IsDeleted)
            .AnyAsync();

        if (duplicateVersion)
        {
            return BadRequest(
                ApiResponse<Recipe>.Fail(
                    $"Ya existe otra receta versión {dto.Version} para este producto"
                )
            );
        }

        var repeatedMaterial = dto.Details
            .GroupBy(item => item.RawMaterialId)
            .FirstOrDefault(group => group.Count() > 1);

        if (repeatedMaterial != null)
        {
            return BadRequest(
                ApiResponse<Recipe>.Fail(
                    "Una materia prima no puede repetirse en la receta"
                )
            );
        }

        var detailsResult =
            await BuildRecipeDetailsAsync(dto.Details);

        if (!detailsResult.Success)
        {
            return BadRequest(
                ApiResponse<Recipe>.Fail(
                    detailsResult.ErrorMessage!
                )
            );
        }

        if (dto.IsActive)
        {
            await DeactivateProductRecipesAsync(
                dto.ProductId,
                id
            );
        }

        recipe.Code =
            $"REC-{product.Slug.ToUpperInvariant()}-V{dto.Version}";

        recipe.ProductId = product.Id;
        recipe.ProductName = product.Name;
        recipe.Version = dto.Version;
        recipe.Notes = dto.Notes.Trim();
        recipe.Details = detailsResult.Details;
        recipe.EstimatedUnitCost =
            detailsResult.Details.Sum(
                item => item.EstimatedSubtotal
            );
        recipe.IsActive = dto.IsActive;
        recipe.UpdatedAt = DateTime.UtcNow;
        recipe.UpdatedBy = GetCurrentUserId();

        await _db.Recipes.ReplaceOneAsync(
            item =>
                item.Id == id &&
                !item.IsDeleted,
            recipe
        );

        return Ok(
            ApiResponse<Recipe>.Ok(
                recipe,
                "Receta actualizada correctamente"
            )
        );
    }

    // =========================================================
    // PATCH: api/Recipes/{id}/status
    // =========================================================
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        string id,
        [FromQuery] bool isActive)
    {
        var recipe = await _db.Recipes
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (recipe == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Receta no encontrada"
                )
            );
        }

        if (isActive)
        {
            await DeactivateProductRecipesAsync(
                recipe.ProductId,
                recipe.Id
            );
        }

        var update = Builders<Recipe>.Update
            .Set(item => item.IsActive, isActive)
            .Set(item => item.UpdatedAt, DateTime.UtcNow)
            .Set(item => item.UpdatedBy, GetCurrentUserId());

        await _db.Recipes.UpdateOneAsync(
            item =>
                item.Id == id &&
                !item.IsDeleted,
            update
        );

        return Ok(
            ApiResponse<string>.Ok(
                isActive
                    ? "Receta activada correctamente"
                    : "Receta desactivada correctamente"
            )
        );
    }

    // =========================================================
    // DELETE: api/Recipes/{id}
    // =========================================================
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var hasProductionOrders =
            await _db.ProductionOrders
                .Find(order =>
                    order.RecipeId == id &&
                    !order.IsDeleted)
                .AnyAsync();

        if (hasProductionOrders)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "No se puede eliminar una receta utilizada en órdenes de producción. Puedes desactivarla."
                )
            );
        }

        var update = Builders<Recipe>.Update
            .Set(item => item.IsDeleted, true)
            .Set(item => item.IsActive, false)
            .Set(item => item.UpdatedAt, DateTime.UtcNow)
            .Set(item => item.UpdatedBy, GetCurrentUserId());

        var result = await _db.Recipes.UpdateOneAsync(
            item =>
                item.Id == id &&
                !item.IsDeleted,
            update
        );

        if (result.MatchedCount == 0)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Receta no encontrada"
                )
            );
        }

        return Ok(
            ApiResponse<string>.Ok(
                "Receta eliminada correctamente"
            )
        );
    }

    private async Task<RecipeDetailsResult>
        BuildRecipeDetailsAsync(
            IEnumerable<RecipeDetailDto> inputDetails)
    {
        var details = new List<RecipeDetail>();

        foreach (var input in inputDetails)
        {
            if (input.QuantityRequired <= 0)
            {
                return RecipeDetailsResult.Fail(
                    "Todas las cantidades requeridas deben ser mayores a cero"
                );
            }

            if (input.WastePercentage < 0 ||
                input.WastePercentage > 100)
            {
                return RecipeDetailsResult.Fail(
                    "El porcentaje de merma debe estar entre 0 y 100"
                );
            }

            var material = await _db.RawMaterials
                .Find(item =>
                    item.Id == input.RawMaterialId &&
                    !item.IsDeleted)
                .FirstOrDefaultAsync();

            if (material == null)
            {
                return RecipeDetailsResult.Fail(
                    $"Materia prima no encontrada: {input.RawMaterialId}"
                );
            }

            if (!material.IsActive)
            {
                return RecipeDetailsResult.Fail(
                    $"La materia prima {material.Name} está inactiva"
                );
            }

            var totalQuantity =
                input.QuantityRequired *
                (
                    1 +
                    input.WastePercentage / 100
                );

            var subtotal =
                totalQuantity *
                material.AverageCost;

            details.Add(
                new RecipeDetail
                {
                    RawMaterialId = material.Id,
                    RawMaterialCode = material.Code,
                    RawMaterialName = material.Name,
                    Unit = material.Unit,
                    QuantityRequired =
                        input.QuantityRequired,
                    WastePercentage =
                        input.WastePercentage,
                    AcceptsRecoveredWaste =
                        input.AcceptsRecoveredWaste,
                    EstimatedUnitCost =
                        material.AverageCost,
                    EstimatedSubtotal = subtotal
                }
            );
        }

        return RecipeDetailsResult.Ok(details);
    }

    private async Task DeactivateProductRecipesAsync(
        string productId,
        string? exceptRecipeId = null)
    {
        var filter =
            Builders<Recipe>.Filter.Eq(
                item => item.ProductId,
                productId
            )
            &
            Builders<Recipe>.Filter.Eq(
                item => item.IsDeleted,
                false
            );

        if (!string.IsNullOrWhiteSpace(
            exceptRecipeId))
        {
            filter &=
                Builders<Recipe>.Filter.Ne(
                    item => item.Id,
                    exceptRecipeId
                );
        }

        var update = Builders<Recipe>.Update
            .Set(item => item.IsActive, false)
            .Set(item => item.UpdatedAt, DateTime.UtcNow);

        await _db.Recipes.UpdateManyAsync(
            filter,
            update
        );
    }

    private static string? ValidateRecipe(
        RecipeCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ProductId))
            return "Debes seleccionar un producto";

        if (dto.Version <= 0)
            return "La versión debe ser mayor a cero";

        if (dto.Details == null ||
            dto.Details.Count == 0)
        {
            return "La receta debe incluir materia prima";
        }

        if (dto.Details.Count > 100)
            return "La receta no puede contener más de 100 componentes";

        if (dto.Notes.Trim().Length > 1000)
            return "Las observaciones no pueden superar los 1000 caracteres";

        return null;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }

    private sealed class RecipeDetailsResult
    {
        public bool Success { get; init; }

        public string? ErrorMessage { get; init; }

        public List<RecipeDetail> Details { get; init; } =
            new();

        public static RecipeDetailsResult Ok(
            List<RecipeDetail> details)
        {
            return new RecipeDetailsResult
            {
                Success = true,
                Details = details
            };
        }

        public static RecipeDetailsResult Fail(
            string message)
        {
            return new RecipeDetailsResult
            {
                Success = false,
                ErrorMessage = message
            };
        }
    }
}