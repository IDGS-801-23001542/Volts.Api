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

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var recipes = await _db.Recipes
            .Find(x => !x.IsDeleted)
            .SortBy(x => x.ProductName)
            .ToListAsync();

        return Ok(ApiResponse<List<Recipe>>.Ok(recipes));
    }

    [HttpGet("product/{productId}")]
    public async Task<IActionResult> GetByProduct(string productId)
    {
        var recipe = await _db.Recipes
            .Find(x => x.ProductId == productId && !x.IsDeleted && x.IsActive)
            .FirstOrDefaultAsync();

        if (recipe == null)
            return NotFound(ApiResponse<Recipe>.Fail("Receta no encontrada"));

        return Ok(ApiResponse<Recipe>.Ok(recipe));
    }

    [HttpPost]
    public async Task<IActionResult> Create(RecipeCreateDto dto)
    {
        var product = await _db.Products.Find(x => x.Id == dto.ProductId && !x.IsDeleted).FirstOrDefaultAsync();

        if (product == null)
            return BadRequest(ApiResponse<Recipe>.Fail("Producto no encontrado"));

        var exists = await _db.Recipes.Find(x => x.ProductId == dto.ProductId && !x.IsDeleted).AnyAsync();

        if (exists)
            return BadRequest(ApiResponse<Recipe>.Fail("Este producto ya tiene receta"));

        if (dto.Details.Count == 0)
            return BadRequest(ApiResponse<Recipe>.Fail("La receta debe tener materia prima"));

        var details = new List<RecipeDetail>();

        foreach (var item in dto.Details)
        {
            var material = await _db.RawMaterials.Find(x => x.Id == item.RawMaterialId && !x.IsDeleted).FirstOrDefaultAsync();

            if (material == null)
                return BadRequest(ApiResponse<Recipe>.Fail($"Materia prima no encontrada: {item.RawMaterialId}"));

            if (item.QuantityRequired <= 0)
                return BadRequest(ApiResponse<Recipe>.Fail("La cantidad requerida debe ser mayor a 0"));

            details.Add(new RecipeDetail
            {
                RawMaterialId = material.Id,
                RawMaterialName = material.Name,
                QuantityRequired = item.QuantityRequired,
                Unit = material.Unit
            });
        }

        var recipe = new Recipe
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Details = details,
            IsActive = true
        };

        await _db.Recipes.InsertOneAsync(recipe);

        return Ok(ApiResponse<Recipe>.Ok(recipe, "Receta creada correctamente"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, RecipeUpdateDto dto)
    {
        var recipe = await _db.Recipes.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();

        if (recipe == null)
            return NotFound(ApiResponse<Recipe>.Fail("Receta no encontrada"));

        var product = await _db.Products.Find(x => x.Id == dto.ProductId && !x.IsDeleted).FirstOrDefaultAsync();

        if (product == null)
            return BadRequest(ApiResponse<Recipe>.Fail("Producto no encontrado"));

        var details = new List<RecipeDetail>();

        foreach (var item in dto.Details)
        {
            var material = await _db.RawMaterials.Find(x => x.Id == item.RawMaterialId && !x.IsDeleted).FirstOrDefaultAsync();

            if (material == null)
                return BadRequest(ApiResponse<Recipe>.Fail($"Materia prima no encontrada: {item.RawMaterialId}"));

            details.Add(new RecipeDetail
            {
                RawMaterialId = material.Id,
                RawMaterialName = material.Name,
                QuantityRequired = item.QuantityRequired,
                Unit = material.Unit
            });
        }

        recipe.ProductId = product.Id;
        recipe.ProductName = product.Name;
        recipe.Details = details;
        recipe.IsActive = dto.IsActive;
        recipe.UpdatedAt = DateTime.UtcNow;

        await _db.Recipes.ReplaceOneAsync(x => x.Id == id, recipe);

        return Ok(ApiResponse<Recipe>.Ok(recipe, "Receta actualizada correctamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<Recipe>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.IsActive, false)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Recipes.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Receta no encontrada"));

        return Ok(ApiResponse<string>.Ok("Receta eliminada correctamente"));
    }
}