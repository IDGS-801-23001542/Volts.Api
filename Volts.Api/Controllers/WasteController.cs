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
public class WasteController : ControllerBase
{
    private readonly MongoDbService _db;

    public WasteController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var wastes = await _db.Wastes
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<Waste>>.Ok(wastes));
    }

    [HttpPost]
    public async Task<IActionResult> Create(WasteCreateDto dto)
    {
        if (dto.Quantity <= 0)
            return BadRequest(ApiResponse<Waste>.Fail("La cantidad debe ser mayor a 0"));

        var material = await _db.RawMaterials.Find(x => x.Id == dto.RawMaterialId && !x.IsDeleted).FirstOrDefaultAsync();

        if (material == null)
            return BadRequest(ApiResponse<Waste>.Fail("Materia prima no encontrada"));

        if (material.CurrentStock < dto.Quantity)
            return BadRequest(ApiResponse<Waste>.Fail("Stock insuficiente para registrar merma"));

        material.CurrentStock -= dto.Quantity;
        material.UpdatedAt = DateTime.UtcNow;

        await _db.RawMaterials.ReplaceOneAsync(x => x.Id == material.Id, material);

        var waste = new Waste
        {
            RawMaterialId = material.Id,
            RawMaterialName = material.Name,
            Quantity = dto.Quantity,
            Reason = dto.Reason
        };

        await _db.Wastes.InsertOneAsync(waste);

        return Ok(ApiResponse<Waste>.Ok(waste, "Merma registrada y stock descontado correctamente"));
    }
}