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

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll()
    {
        var categories = await _db.Categories
            .Find(x => !x.IsDeleted && x.IsActive)
            .SortBy(x => x.Name)
            .ToListAsync();

        return Ok(ApiResponse<List<Category>>.Ok(categories));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(string id)
    {
        var category = await _db.Categories
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (category == null)
            return NotFound(ApiResponse<Category>.Fail("Categoría no encontrada"));

        return Ok(ApiResponse<Category>.Ok(category));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CategoryCreateDto dto)
    {
        var exists = await _db.Categories
            .Find(x => x.Name.ToLower() == dto.Name.ToLower() && !x.IsDeleted)
            .AnyAsync();

        if (exists)
            return BadRequest(ApiResponse<Category>.Fail("Ya existe una categoría con ese nombre"));

        var category = new Category
        {
            Name = dto.Name,
            Description = dto.Description,
            IsActive = true
        };

        await _db.Categories.InsertOneAsync(category);

        return Ok(ApiResponse<Category>.Ok(category, "Categoría creada correctamente"));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, CategoryUpdateDto dto)
    {
        var category = await _db.Categories
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (category == null)
            return NotFound(ApiResponse<Category>.Fail("Categoría no encontrada"));

        category.Name = dto.Name;
        category.Description = dto.Description;
        category.IsActive = dto.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        await _db.Categories.ReplaceOneAsync(x => x.Id == id, category);

        return Ok(ApiResponse<Category>.Ok(category, "Categoría actualizada correctamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<Category>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.IsActive, false)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Categories.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Categoría no encontrada"));

        return Ok(ApiResponse<string>.Ok("Categoría eliminada correctamente"));
    }
}