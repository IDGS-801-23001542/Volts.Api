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
public class SuppliersController : ControllerBase
{
    private readonly MongoDbService _db;

    public SuppliersController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var suppliers = await _db.Suppliers
            .Find(x => !x.IsDeleted)
            .SortBy(x => x.Name)
            .ToListAsync();

        return Ok(ApiResponse<List<Supplier>>.Ok(suppliers));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SupplierCreateDto dto)
    {
        var supplier = new Supplier
        {
            Name = dto.Name,
            ContactName = dto.ContactName,
            Email = dto.Email.ToLower(),
            Phone = dto.Phone,
            Address = dto.Address,
            IsActive = true
        };

        await _db.Suppliers.InsertOneAsync(supplier);

        return Ok(ApiResponse<Supplier>.Ok(supplier, "Proveedor creado correctamente"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, SupplierUpdateDto dto)
    {
        var supplier = await _db.Suppliers.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();

        if (supplier == null)
            return NotFound(ApiResponse<Supplier>.Fail("Proveedor no encontrado"));

        supplier.Name = dto.Name;
        supplier.ContactName = dto.ContactName;
        supplier.Email = dto.Email.ToLower();
        supplier.Phone = dto.Phone;
        supplier.Address = dto.Address;
        supplier.IsActive = dto.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;

        await _db.Suppliers.ReplaceOneAsync(x => x.Id == id, supplier);

        return Ok(ApiResponse<Supplier>.Ok(supplier, "Proveedor actualizado correctamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<Supplier>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.IsActive, false)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Suppliers.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Proveedor no encontrado"));

        return Ok(ApiResponse<string>.Ok("Proveedor eliminado correctamente"));
    }
}