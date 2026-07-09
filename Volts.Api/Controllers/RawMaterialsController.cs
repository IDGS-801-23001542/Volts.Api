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
public class RawMaterialsController : ControllerBase
{
    private readonly MongoDbService _db;

    public RawMaterialsController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var materials = await _db.RawMaterials
            .Find(x => !x.IsDeleted)
            .SortBy(x => x.Name)
            .ToListAsync();

        return Ok(ApiResponse<List<RawMaterial>>.Ok(materials));
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock()
    {
        var materials = await _db.RawMaterials
            .Find(x => !x.IsDeleted && x.CurrentStock <= x.MinimumStock)
            .SortBy(x => x.Name)
            .ToListAsync();

        return Ok(ApiResponse<List<RawMaterial>>.Ok(materials));
    }

    [HttpPost]
    public async Task<IActionResult> Create(RawMaterialCreateDto dto)
    {
        var material = new RawMaterial
        {
            Name = dto.Name,
            Unit = dto.Unit,
            CurrentStock = dto.CurrentStock,
            MinimumStock = dto.MinimumStock,
            MaximumStock = dto.MaximumStock,
            AverageCost = dto.AverageCost,
            IsActive = true
        };

        await _db.RawMaterials.InsertOneAsync(material);

        return Ok(ApiResponse<RawMaterial>.Ok(material, "Materia prima creada correctamente"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, RawMaterialUpdateDto dto)
    {
        var material = await _db.RawMaterials.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();

        if (material == null)
            return NotFound(ApiResponse<RawMaterial>.Fail("Materia prima no encontrada"));

        material.Name = dto.Name;
        material.Unit = dto.Unit;
        material.CurrentStock = dto.CurrentStock;
        material.MinimumStock = dto.MinimumStock;
        material.MaximumStock = dto.MaximumStock;
        material.AverageCost = dto.AverageCost;
        material.IsActive = dto.IsActive;
        material.UpdatedAt = DateTime.UtcNow;

        await _db.RawMaterials.ReplaceOneAsync(x => x.Id == id, material);

        return Ok(ApiResponse<RawMaterial>.Ok(material, "Materia prima actualizada correctamente"));
    }

    [HttpPut("{id}/add-stock")]
    public async Task<IActionResult> AddStock(string id, RawMaterialStockUpdateDto dto)
    {
        if (dto.Quantity <= 0)
            return BadRequest(ApiResponse<string>.Fail("La cantidad debe ser mayor a 0"));

        var update = Builders<RawMaterial>.Update
            .Inc(x => x.CurrentStock, dto.Quantity)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.RawMaterials.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Materia prima no encontrada"));

        return Ok(ApiResponse<string>.Ok("Stock agregado correctamente"));
    }

    [HttpPut("{id}/remove-stock")]
    public async Task<IActionResult> RemoveStock(string id, RawMaterialStockUpdateDto dto)
    {
        var material = await _db.RawMaterials.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();

        if (material == null)
            return NotFound(ApiResponse<string>.Fail("Materia prima no encontrada"));

        if (dto.Quantity <= 0)
            return BadRequest(ApiResponse<string>.Fail("La cantidad debe ser mayor a 0"));

        if (material.CurrentStock < dto.Quantity)
            return BadRequest(ApiResponse<string>.Fail("Stock insuficiente"));

        material.CurrentStock -= dto.Quantity;
        material.UpdatedAt = DateTime.UtcNow;

        await _db.RawMaterials.ReplaceOneAsync(x => x.Id == id, material);

        return Ok(ApiResponse<string>.Ok("Stock descontado correctamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<RawMaterial>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.IsActive, false)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.RawMaterials.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Materia prima no encontrada"));

        return Ok(ApiResponse<string>.Ok("Materia prima eliminada correctamente"));
    }
}