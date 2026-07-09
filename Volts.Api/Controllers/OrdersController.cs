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
public class OrdersController : ControllerBase
{
    private readonly MongoDbService _db;

    public OrdersController(MongoDbService db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orders = await _db.Orders
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<Order>>.Ok(orders));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var order = await _db.Orders
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (order == null)
            return NotFound(ApiResponse<Order>.Fail("Pedido no encontrado"));

        return Ok(ApiResponse<Order>.Ok(order));
    }

    [HttpPost]
    public async Task<IActionResult> Create(OrderCreateDto dto)
    {
        var customer = await _db.Customers
            .Find(x => x.Id == dto.CustomerId && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (customer == null)
            return BadRequest(ApiResponse<Order>.Fail("Cliente no encontrado"));

        if (dto.Details.Count == 0)
            return BadRequest(ApiResponse<Order>.Fail("El pedido debe tener productos"));

        var details = new List<OrderDetail>();
        decimal total = 0;

        foreach (var item in dto.Details)
        {
            var product = await _db.Products
                .Find(x => x.Id == item.ProductId && !x.IsDeleted && x.IsActive)
                .FirstOrDefaultAsync();

            if (product == null)
                return BadRequest(ApiResponse<Order>.Fail($"Producto no encontrado: {item.ProductId}"));

            var subtotal = product.Price * item.Quantity;

            details.Add(new OrderDetail
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                Subtotal = subtotal
            });

            total += subtotal;
        }

        var order = new Order
        {
            CustomerId = customer.Id,
            CustomerName = customer.FullName,
            Status = "Pending",
            Total = total,
            Details = details
        };

        await _db.Orders.InsertOneAsync(order);

        return Ok(ApiResponse<Order>.Ok(order, "Pedido creado correctamente"));
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, OrderStatusUpdateDto dto)
    {
        var allowed = new[] { "Pending", "Confirmed", "InProduction", "Shipped", "Delivered", "Cancelled" };

        if (!allowed.Contains(dto.Status))
            return BadRequest(ApiResponse<string>.Fail("Estado de pedido inválido"));

        var update = Builders<Order>.Update
            .Set(x => x.Status, dto.Status)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Orders.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Pedido no encontrado"));

        return Ok(ApiResponse<string>.Ok("Estado actualizado correctamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var update = Builders<Order>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _db.Orders.UpdateOneAsync(x => x.Id == id && !x.IsDeleted, update);

        if (result.ModifiedCount == 0)
            return NotFound(ApiResponse<string>.Fail("Pedido no encontrado"));

        return Ok(ApiResponse<string>.Ok("Pedido eliminado correctamente"));
    }
}