using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Volts.Api.DTOs;
using Volts.Api.Models;
using Volts.Api.Models.Enums;
using Volts.Api.Responses;
using Volts.Api.Services;

namespace Volts.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Employee")]
public class RecipesController : ControllerBase
{
    private readonly MongoDbService _db;
    public RecipesController(MongoDbService db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(
        ApiResponse<List<Recipe>>.Ok(
            await _db.Recipes.Find(x => !x.IsDeleted)
                .SortBy(x => x.ProductName).ThenByDescending(x => x.Version).ToListAsync(),
            "Recetas obtenidas correctamente"));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var recipe = await _db.Recipes.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        return recipe == null
            ? NotFound(ApiResponse<Recipe>.Fail("Receta no encontrada"))
            : Ok(ApiResponse<Recipe>.Ok(recipe, "Receta obtenida correctamente"));
    }

    [HttpGet("product/{productId}")]
    public async Task<IActionResult> GetByProduct(string productId)
    {
        var recipe = await _db.Recipes.Find(x => x.ProductId == productId && !x.IsDeleted && x.Status == RecipeStatus.Active)
            .SortByDescending(x => x.Version).FirstOrDefaultAsync();
        return recipe == null
            ? NotFound(ApiResponse<Recipe>.Fail("El producto no tiene una receta activa"))
            : Ok(ApiResponse<Recipe>.Ok(recipe, "Receta activa obtenida correctamente"));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RecipeCreateDto dto)
    {
        var error = Validate(dto);
        if (error != null) return BadRequest(ApiResponse<Recipe>.Fail(error));

        var product = await _db.Products.Find(x => x.Id == dto.ProductId && !x.IsDeleted && x.IsActive).FirstOrDefaultAsync();
        if (product == null) return BadRequest(ApiResponse<Recipe>.Fail("Producto no encontrado o inactivo"));
        if (!product.CanBeProduced) return BadRequest(ApiResponse<Recipe>.Fail("El producto no está habilitado para producción"));

        if (await _db.Recipes.Find(x => x.ProductId == dto.ProductId && x.Version == dto.Version && !x.IsDeleted).AnyAsync())
            return BadRequest(ApiResponse<Recipe>.Fail($"Ya existe la versión {dto.Version} para este producto"));

        if (dto.Details.GroupBy(x => x.RawMaterialId).Any(g => g.Count() > 1))
            return BadRequest(ApiResponse<Recipe>.Fail("Una materia prima no puede repetirse en la receta"));

        var built = await BuildDetailsAsync(dto.Details);
        if (built.Error != null) return BadRequest(ApiResponse<Recipe>.Fail(built.Error));

        using var session = await _db.StartSessionAsync();
        session.StartTransaction();
        try
        {
            if (dto.Status == RecipeStatus.Active)
                await DeactivateAsync(session, product.Id, null);

            var recipe = new Recipe
            {
                Code = $"REC-{product.Slug.ToUpperInvariant()}-V{dto.Version}",
                ProductId = product.Id,
                ProductName = product.Name,
                Version = dto.Version,
                Notes = dto.Notes.Trim(),
                Status = dto.Status,
                IsActive = dto.Status == RecipeStatus.Active,
                Details = built.Details,
                EstimatedUnitCost = InventoryRoundingService.RoundEstimatedCost(built.Details.Sum(x => x.EstimatedSubtotal)),
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
            };
            await _db.Recipes.InsertOneAsync(session, recipe);
            await session.CommitTransactionAsync();
            return CreatedAtAction(nameof(GetById), new { id = recipe.Id }, ApiResponse<Recipe>.Ok(recipe, "Receta creada correctamente"));
        }
        catch { await session.AbortTransactionAsync(); throw; }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] RecipeUpdateDto dto)
    {
        var error = Validate(dto);
        if (error != null) return BadRequest(ApiResponse<Recipe>.Fail(error));
        var recipe = await _db.Recipes.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (recipe == null) return NotFound(ApiResponse<Recipe>.Fail("Receta no encontrada"));
        if (await _db.ProductionOrders.Find(x => x.RecipeId == id && !x.IsDeleted).AnyAsync())
            return BadRequest(ApiResponse<Recipe>.Fail("Una receta utilizada en producción ya no puede editarse; crea una nueva versión"));

        var product = await _db.Products.Find(x => x.Id == dto.ProductId && !x.IsDeleted && x.IsActive).FirstOrDefaultAsync();
        if (product == null) return BadRequest(ApiResponse<Recipe>.Fail("Producto no encontrado o inactivo"));
        if (await _db.Recipes.Find(x => x.Id != id && x.ProductId == dto.ProductId && x.Version == dto.Version && !x.IsDeleted).AnyAsync())
            return BadRequest(ApiResponse<Recipe>.Fail("La versión ya existe"));
        if (dto.Details.GroupBy(x => x.RawMaterialId).Any(g => g.Count() > 1))
            return BadRequest(ApiResponse<Recipe>.Fail("Una materia prima no puede repetirse"));

        var built = await BuildDetailsAsync(dto.Details);
        if (built.Error != null) return BadRequest(ApiResponse<Recipe>.Fail(built.Error));

        using var session = await _db.StartSessionAsync();
        session.StartTransaction();
        try
        {
            if (dto.Status == RecipeStatus.Active)
                await DeactivateAsync(session, product.Id, id);

            recipe.Code = $"REC-{product.Slug.ToUpperInvariant()}-V{dto.Version}";
            recipe.ProductId = product.Id;
            recipe.ProductName = product.Name;
            recipe.Version = dto.Version;
            recipe.Notes = dto.Notes.Trim();
            recipe.Status = dto.Status;
            recipe.IsActive = dto.Status == RecipeStatus.Active;
            recipe.Details = built.Details;
            recipe.EstimatedUnitCost = InventoryRoundingService.RoundEstimatedCost(built.Details.Sum(x => x.EstimatedSubtotal));
            recipe.UpdatedAt = DateTime.UtcNow;
            recipe.UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _db.Recipes.ReplaceOneAsync(session, x => x.Id == id && !x.IsDeleted, recipe);
            await session.CommitTransactionAsync();
            return Ok(ApiResponse<Recipe>.Ok(recipe, "Receta actualizada correctamente"));
        }
        catch { await session.AbortTransactionAsync(); throw; }
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, [FromQuery] RecipeStatus status)
    {
        var recipe = await _db.Recipes.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (recipe == null) return NotFound(ApiResponse<string>.Fail("Receta no encontrada"));
        using var session = await _db.StartSessionAsync(); session.StartTransaction();
        try
        {
            if (status == RecipeStatus.Active) await DeactivateAsync(session, recipe.ProductId, id);
            recipe.Status = status; recipe.IsActive = status == RecipeStatus.Active;
            recipe.UpdatedAt = DateTime.UtcNow; recipe.UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _db.Recipes.ReplaceOneAsync(session, x => x.Id == id && !x.IsDeleted, recipe);
            await session.CommitTransactionAsync();
            return Ok(ApiResponse<string>.Ok("Estado de receta actualizado correctamente"));
        }
        catch { await session.AbortTransactionAsync(); throw; }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        if (await _db.ProductionOrders.Find(x => x.RecipeId == id && !x.IsDeleted).AnyAsync())
            return BadRequest(ApiResponse<string>.Fail("No se puede eliminar una receta utilizada en producción"));
        var result = await _db.Recipes.UpdateOneAsync(x => x.Id == id && !x.IsDeleted,
            Builders<Recipe>.Update.Set(x => x.IsDeleted, true).Set(x => x.IsActive, false).Set(x => x.Status, RecipeStatus.Archived)
                .Set(x => x.UpdatedAt, DateTime.UtcNow).Set(x => x.UpdatedBy, User.FindFirstValue(ClaimTypes.NameIdentifier)));
        return result.MatchedCount == 0 ? NotFound(ApiResponse<string>.Fail("Receta no encontrada"))
            : Ok(ApiResponse<string>.Ok("Receta eliminada correctamente"));
    }

    private async Task<(List<RecipeDetail> Details, string? Error)> BuildDetailsAsync(IEnumerable<RecipeDetailDto> inputs)
    {
        var list = new List<RecipeDetail>();
        foreach (var input in inputs)
        {
            var material = await _db.RawMaterials.Find(x => x.Id == input.RawMaterialId && !x.IsDeleted && x.IsActive).FirstOrDefaultAsync();
            if (material == null) return (list, "Una materia prima no existe o está inactiva");
            var unit = new UnitOfMeasure { Symbol = material.UnitSymbol, SingularName = material.UnitName, AllowsDecimals = material.UnitAllowsDecimals, DecimalPlaces = material.UnitDecimalPlaces };
            var quantityError = QuantityValidationService.ValidateQuantity(input.QuantityRequired, unit, $"La cantidad de {material.Name}");
            if (quantityError != null) return (list, quantityError);
            var wasteError = QuantityValidationService.ValidatePercentage(input.WastePercentage, $"La merma de {material.Name}");
            if (wasteError != null) return (list, wasteError);
            var total = decimal.Round(input.QuantityRequired * (1 + input.WastePercentage / 100m), material.UnitDecimalPlaces, MidpointRounding.AwayFromZero);
            if (!material.UnitAllowsDecimals && decimal.Truncate(total) != total)
                return (list, $"La merma esperada genera una fracción inválida de {material.Name}; ajusta cantidad o porcentaje");
            var subtotal = InventoryRoundingService.RoundEstimatedCost(total * material.AverageCost);
            list.Add(new RecipeDetail
            {
                RawMaterialId = material.Id, RawMaterialCode = material.Code, RawMaterialName = material.Name,
                UnitOfMeasureId = material.UnitOfMeasureId, UnitCode = material.UnitCode, UnitName = material.UnitName,
                UnitSymbol = material.UnitSymbol, UnitAllowsDecimals = material.UnitAllowsDecimals,
                UnitDecimalPlaces = material.UnitDecimalPlaces, Unit = material.UnitSymbol,
                QuantityRequired = input.QuantityRequired, WastePercentage = input.WastePercentage,
                TotalQuantityPerUnit = total, AcceptsRecoveredWaste = input.AcceptsRecoveredWaste,
                EstimatedUnitCost = material.AverageCost, EstimatedSubtotal = subtotal
            });
        }
        return (list, null);
    }

    private async Task DeactivateAsync(IClientSessionHandle session, string productId, string? exceptId)
    {
        var filter = Builders<Recipe>.Filter.Eq(x => x.ProductId, productId) & Builders<Recipe>.Filter.Eq(x => x.IsDeleted, false);
        if (!string.IsNullOrWhiteSpace(exceptId)) filter &= Builders<Recipe>.Filter.Ne(x => x.Id, exceptId);
        await _db.Recipes.UpdateManyAsync(session, filter,
            Builders<Recipe>.Update.Set(x => x.Status, RecipeStatus.Archived).Set(x => x.IsActive, false).Set(x => x.UpdatedAt, DateTime.UtcNow));
    }

    private static string? Validate(RecipeCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ProductId)) return "Debes seleccionar un producto";
        if (dto.Version <= 0) return "La versión debe ser mayor a cero";
        if (dto.Details == null || dto.Details.Count == 0) return "La receta debe incluir materia prima";
        if (dto.Details.Count > 100) return "La receta no puede contener más de 100 componentes";
        if ((dto.Notes ?? string.Empty).Trim().Length > 1000) return "Las observaciones no pueden superar 1000 caracteres";
        return null;
    }
}
