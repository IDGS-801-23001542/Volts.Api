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
    private static readonly string[] AllowedStatuses =
    {
        "Pending",
        "Confirmed",
        "InProduction",
        "Shipped",
        "Delivered",
        "Cancelled"
    };

    private readonly MongoDbService _db;

    public OrdersController(MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // GET: api/Orders
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orders = await _db.Orders
            .Find(order => !order.IsDeleted)
            .SortByDescending(order => order.CreatedAt)
            .ToListAsync();

        return Ok(
            ApiResponse<List<Order>>.Ok(
                orders,
                "Pedidos obtenidos correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/Orders/{id}
    // =========================================================
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<Order>.Fail(
                    "El identificador del pedido es obligatorio"
                )
            );
        }

        var order = await _db.Orders
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (order == null)
        {
            return NotFound(
                ApiResponse<Order>.Fail(
                    "Pedido no encontrado"
                )
            );
        }

        return Ok(
            ApiResponse<Order>.Ok(
                order,
                "Pedido obtenido correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/Orders
    //
    // El pedido valida disponibilidad e inventario, pero todavía
    // no descuenta existencias. El descuento se realizará cuando
    // exista el flujo definitivo de confirmación o venta.
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] OrderCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerId))
        {
            return BadRequest(
                ApiResponse<Order>.Fail(
                    "Debes seleccionar un cliente"
                )
            );
        }

        if (dto.Details == null || dto.Details.Count == 0)
        {
            return BadRequest(
                ApiResponse<Order>.Fail(
                    "El pedido debe contener al menos un producto"
                )
            );
        }

        var customer = await _db.Customers
            .Find(item =>
                item.Id == dto.CustomerId &&
                !item.IsDeleted &&
                item.IsActive)
            .FirstOrDefaultAsync();

        if (customer == null)
        {
            return BadRequest(
                ApiResponse<Order>.Fail(
                    "El cliente no existe o está inactivo"
                )
            );
        }

        /*
         * Se agrupan productos repetidos para validar correctamente
         * el total solicitado de cada producto.
         */
        var groupedItems = dto.Details
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity)
            })
            .ToList();

        if (groupedItems.Any(item =>
            string.IsNullOrWhiteSpace(item.ProductId)))
        {
            return BadRequest(
                ApiResponse<Order>.Fail(
                    "Todos los productos deben tener un identificador válido"
                )
            );
        }

        if (groupedItems.Any(item => item.Quantity <= 0))
        {
            return BadRequest(
                ApiResponse<Order>.Fail(
                    "Todas las cantidades deben ser mayores a cero"
                )
            );
        }

        var details = new List<OrderDetail>();
        decimal total = 0;

        foreach (var item in groupedItems)
        {
            var product = await _db.Products
                .Find(productItem =>
                    productItem.Id == item.ProductId &&
                    !productItem.IsDeleted &&
                    productItem.IsActive)
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return BadRequest(
                    ApiResponse<Order>.Fail(
                        $"Producto no encontrado: {item.ProductId}"
                    )
                );
            }

            if (!product.CanBePurchased ||
                product.CommercialStatus != "Available")
            {
                return BadRequest(
                    ApiResponse<Order>.Fail(
                        $"El producto {product.Name} todavía no está disponible para compra"
                    )
                );
            }

            if (product.FinishedStock < item.Quantity)
            {
                return BadRequest(
                    ApiResponse<Order>.Fail(
                        $"No hay inventario suficiente de {product.Name}. " +
                        $"Solicitado: {item.Quantity}. " +
                        $"Disponible: {product.FinishedStock}"
                    )
                );
            }

            var subtotal =
                product.Price * item.Quantity;

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
            Details = details,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        await _db.Orders.InsertOneAsync(order);

        return CreatedAtAction(
            nameof(GetById),
            new { id = order.Id },
            ApiResponse<Order>.Ok(
                order,
                "Pedido creado correctamente"
            )
        );
    }

    // =========================================================
    // PUT: api/Orders/{id}/status
    // =========================================================
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        string id,
        [FromBody] OrderStatusUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "El identificador del pedido es obligatorio"
                )
            );
        }

        var normalizedStatus = dto.Status?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedStatus) ||
            !AllowedStatuses.Contains(normalizedStatus))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Estado de pedido inválido"
                )
            );
        }

        var order = await _db.Orders
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (order == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Pedido no encontrado"
                )
            );
        }

        if (order.Status == "Cancelled" &&
            normalizedStatus != "Cancelled")
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Un pedido cancelado no puede cambiar nuevamente de estado"
                )
            );
        }

        if (order.Status == "Delivered" &&
            normalizedStatus != "Delivered")
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "Un pedido entregado no puede regresar a otro estado"
                )
            );
        }

        var update = Builders<Order>.Update
            .Set(item => item.Status, normalizedStatus)
            .Set(item => item.UpdatedAt, DateTime.UtcNow);

        await _db.Orders.UpdateOneAsync(
            item =>
                item.Id == id &&
                !item.IsDeleted,
            update
        );

        return Ok(
            ApiResponse<string>.Ok(
                "Estado del pedido actualizado correctamente"
            )
        );
    }

    // =========================================================
    // DELETE: api/Orders/{id}
    // Solo Admin. Eliminación lógica.
    // =========================================================
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "El identificador del pedido es obligatorio"
                )
            );
        }

        var order = await _db.Orders
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (order == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Pedido no encontrado"
                )
            );
        }

        if (order.Status == "Shipped" ||
            order.Status == "Delivered")
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "No se puede eliminar un pedido enviado o entregado"
                )
            );
        }

        var update = Builders<Order>.Update
            .Set(item => item.IsDeleted, true)
            .Set(item => item.UpdatedAt, DateTime.UtcNow);

        await _db.Orders.UpdateOneAsync(
            item =>
                item.Id == id &&
                !item.IsDeleted,
            update
        );

        return Ok(
            ApiResponse<string>.Ok(
                "Pedido eliminado correctamente"
            )
        );
    }
}