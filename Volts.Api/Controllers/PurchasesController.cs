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
public class PurchasesController : ControllerBase
{
    private readonly MongoDbService _db;

    public PurchasesController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var purchases = await _db.Purchases
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.PurchaseDate)
            .ToListAsync();

        return Ok(ApiResponse<List<Purchase>>.Ok(purchases));
    }

    [HttpPost]
    public async Task<IActionResult> Create(PurchaseCreateDto dto)
    {
        var supplier = await _db.Suppliers.Find(x => x.Id == dto.SupplierId && !x.IsDeleted).FirstOrDefaultAsync();

        if (supplier == null)
            return BadRequest(ApiResponse<Purchase>.Fail("Proveedor no encontrado"));

        if (dto.Details.Count == 0)
            return BadRequest(ApiResponse<Purchase>.Fail("La compra debe tener detalles"));

        var details = new List<PurchaseDetail>();
        decimal total = 0;

        foreach (var item in dto.Details)
        {
            var material = await _db.RawMaterials.Find(x => x.Id == item.RawMaterialId && !x.IsDeleted).FirstOrDefaultAsync();

            if (material == null)
                return BadRequest(ApiResponse<Purchase>.Fail($"Materia prima no encontrada: {item.RawMaterialId}"));

            if (item.Quantity <= 0 || item.UnitCost < 0)
                return BadRequest(ApiResponse<Purchase>.Fail("Cantidad o costo inválido"));

            var subtotal = item.Quantity * item.UnitCost;

            details.Add(new PurchaseDetail
            {
                RawMaterialId = material.Id,
                RawMaterialName = material.Name,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
                Subtotal = subtotal
            });

            var newStock = material.CurrentStock + item.Quantity;
            var newAverageCost = newStock == 0
                ? item.UnitCost
                : ((material.CurrentStock * material.AverageCost) + subtotal) / newStock;

            material.CurrentStock = newStock;
            material.AverageCost = newAverageCost;
            material.UpdatedAt = DateTime.UtcNow;

            await _db.RawMaterials.ReplaceOneAsync(x => x.Id == material.Id, material);

            total += subtotal;
        }

        var purchase = new Purchase
        {
            SupplierId = supplier.Id,
            SupplierName = supplier.Name,
            PurchaseDate = DateTime.UtcNow,
            Total = total,
            Status = "Completed",
            Details = details
        };

        await _db.Purchases.InsertOneAsync(purchase);

        return Ok(ApiResponse<Purchase>.Ok(purchase, "Compra registrada y stock actualizado correctamente"));
    }
}