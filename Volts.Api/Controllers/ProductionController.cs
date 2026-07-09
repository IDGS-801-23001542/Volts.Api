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
public class ProductionController : ControllerBase
{
    private readonly MongoDbService _db;

    public ProductionController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orders = await _db.ProductionOrders
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<ProductionOrder>>.Ok(orders));
    }

    [HttpPost]
    public async Task<IActionResult> Create(ProductionCreateDto dto)
    {
        if (dto.Quantity <= 0)
            return BadRequest(ApiResponse<ProductionOrder>.Fail("La cantidad debe ser mayor a 0"));

        var product = await _db.Products.Find(x => x.Id == dto.ProductId && !x.IsDeleted).FirstOrDefaultAsync();

        if (product == null)
            return BadRequest(ApiResponse<ProductionOrder>.Fail("Producto no encontrado"));

        var recipe = await _db.Recipes
            .Find(x => x.ProductId == dto.ProductId && !x.IsDeleted && x.IsActive)
            .FirstOrDefaultAsync();

        if (recipe == null)
            return BadRequest(ApiResponse<ProductionOrder>.Fail("El producto no tiene receta activa"));

        foreach (var detail in recipe.Details)
        {
            var material = await _db.RawMaterials.Find(x => x.Id == detail.RawMaterialId && !x.IsDeleted).FirstOrDefaultAsync();

            if (material == null)
                return BadRequest(ApiResponse<ProductionOrder>.Fail($"Materia prima no encontrada: {detail.RawMaterialName}"));

            var required = detail.QuantityRequired * dto.Quantity;

            if (material.CurrentStock < required)
                return BadRequest(ApiResponse<ProductionOrder>.Fail($"Stock insuficiente de {material.Name}. Requerido: {required} {material.Unit}, disponible: {material.CurrentStock} {material.Unit}"));
        }

        foreach (var detail in recipe.Details)
        {
            var required = detail.QuantityRequired * dto.Quantity;

            var update = Builders<RawMaterial>.Update
                .Inc(x => x.CurrentStock, -required)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            await _db.RawMaterials.UpdateOneAsync(x => x.Id == detail.RawMaterialId && !x.IsDeleted, update);
        }

        var production = new ProductionOrder
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Quantity = dto.Quantity,
            Status = "Completed",
            CompletedAt = DateTime.UtcNow
        };

        await _db.ProductionOrders.InsertOneAsync(production);

        return Ok(ApiResponse<ProductionOrder>.Ok(production, "Producción registrada y materia prima descontada correctamente"));
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, ProductionStatusUpdateDto dto)
    {
        var allowed = new[] { "Created", "InProgress", "Completed", "Cancelled" };

        if (!allowed.Contains(dto.Status))
            return BadRequest(ApiResponse<string>.Fail("Estado inválido"));

        var update = Builders<ProductionOrder>.Update
            .Set(x => x.Status, dto.Status)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        if (dto.Status == "Completed")
            update = update.Set(x => x.CompletedAt, DateTime.UtcNow);

        var result = await _db.ProductionOrders.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Orden de producción no encontrada"));

        return Ok(ApiResponse<string>.Ok("Estado actualizado correctamente"));
    }
}