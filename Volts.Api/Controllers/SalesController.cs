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
public class SalesController : ControllerBase
{
    private readonly MongoDbService _db;

    public SalesController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var sales = await _db.Sales
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.SaleDate)
            .ToListAsync();

        return Ok(ApiResponse<List<Sale>>.Ok(sales));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var sale = await _db.Sales
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (sale == null)
            return NotFound(ApiResponse<Sale>.Fail("Venta no encontrada"));

        return Ok(ApiResponse<Sale>.Ok(sale));
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaleCreateDto dto)
    {
        var customer = await _db.Customers
            .Find(x => x.Id == dto.CustomerId && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (customer == null)
            return BadRequest(ApiResponse<Sale>.Fail("Cliente no encontrado"));

        if (dto.Details.Count == 0)
            return BadRequest(ApiResponse<Sale>.Fail("La venta debe tener productos"));

        var details = new List<SaleDetail>();
        decimal total = 0;

        foreach (var item in dto.Details)
        {
            var product = await _db.Products
                .Find(x => x.Id == item.ProductId && !x.IsDeleted && x.IsActive)
                .FirstOrDefaultAsync();

            if (product == null)
                return BadRequest(ApiResponse<Sale>.Fail($"Producto no encontrado: {item.ProductId}"));

            var subtotal = product.Price * item.Quantity;

            details.Add(new SaleDetail
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                Subtotal = subtotal
            });

            total += subtotal;
        }

        var sale = new Sale
        {
            CustomerId = customer.Id,
            CustomerName = customer.FullName,
            SaleDate = DateTime.UtcNow,
            Total = total,
            Details = details
        };

        await _db.Sales.InsertOneAsync(sale);

        return Ok(ApiResponse<Sale>.Ok(sale, "Venta creada correctamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<Sale>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Sales.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Venta no encontrada"));

        return Ok(ApiResponse<string>.Ok("Venta eliminada correctamente"));
    }
}