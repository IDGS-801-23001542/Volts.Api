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

    // =========================================================
    // GET: api/Sales
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var sales = await _db.Sales
            .Find(sale => !sale.IsDeleted)
            .SortByDescending(sale => sale.SaleDate)
            .ToListAsync();

        return Ok(
            ApiResponse<List<Sale>>.Ok(
                sales,
                "Ventas obtenidas correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/Sales/{id}
    // =========================================================
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<Sale>.Fail(
                    "El identificador de la venta es obligatorio"
                )
            );
        }

        var sale = await _db.Sales
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (sale == null)
        {
            return NotFound(
                ApiResponse<Sale>.Fail(
                    "Venta no encontrada"
                )
            );
        }

        return Ok(
            ApiResponse<Sale>.Ok(
                sale,
                "Venta obtenida correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/Sales
    //
    // La venta valida disponibilidad y descuenta inventario
    // terminado de cada producto.
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaleCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerId))
        {
            return BadRequest(
                ApiResponse<Sale>.Fail(
                    "Debes seleccionar un cliente"
                )
            );
        }

        if (dto.Details == null || dto.Details.Count == 0)
        {
            return BadRequest(
                ApiResponse<Sale>.Fail(
                    "La venta debe contener al menos un producto"
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
                ApiResponse<Sale>.Fail(
                    "El cliente no existe o está inactivo"
                )
            );
        }

        /*
         * Agrupa productos repetidos para evitar que se valide
         * el mismo inventario por separado.
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
                ApiResponse<Sale>.Fail(
                    "Todos los productos deben tener un identificador válido"
                )
            );
        }

        if (groupedItems.Any(item => item.Quantity <= 0))
        {
            return BadRequest(
                ApiResponse<Sale>.Fail(
                    "Todas las cantidades deben ser mayores a cero"
                )
            );
        }

        /*
         * Primero se validan todos los productos.
         * Después se realizan los descuentos.
         */
        var validatedProducts =
            new List<(Product Product, int Quantity)>();

        var details = new List<SaleDetail>();
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
                    ApiResponse<Sale>.Fail(
                        $"Producto no encontrado: {item.ProductId}"
                    )
                );
            }

            if (!product.CanBePurchased ||
                product.CommercialStatus != "Available")
            {
                return BadRequest(
                    ApiResponse<Sale>.Fail(
                        $"El producto {product.Name} no está disponible para venta"
                    )
                );
            }

            if (product.FinishedStock < item.Quantity)
            {
                return BadRequest(
                    ApiResponse<Sale>.Fail(
                        $"Stock insuficiente de {product.Name}. " +
                        $"Solicitado: {item.Quantity}. " +
                        $"Disponible: {product.FinishedStock}"
                    )
                );
            }

            var subtotal =
                product.Price * item.Quantity;

            details.Add(new SaleDetail
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                Subtotal = subtotal
            });

            validatedProducts.Add(
                (product, item.Quantity)
            );

            total += subtotal;
        }

        /*
         * Descuento de producto terminado.
         *
         * El filtro también comprueba el stock para disminuir
         * el riesgo de vender unidades que ya fueron tomadas por
         * otra solicitud.
         */
        foreach (var validatedItem in validatedProducts)
        {
            var stockUpdate = Builders<Product>.Update
                .Inc(
                    product =>
                        product.FinishedStock,
                    -validatedItem.Quantity
                )
                .Set(
                    product =>
                        product.UpdatedAt,
                    DateTime.UtcNow
                );

            var stockResult =
                await _db.Products.UpdateOneAsync(
                    product =>
                        product.Id ==
                            validatedItem.Product.Id &&
                        !product.IsDeleted &&
                        product.IsActive &&
                        product.CanBePurchased &&
                        product.CommercialStatus ==
                            "Available" &&
                        product.FinishedStock >=
                            validatedItem.Quantity,
                    stockUpdate
                );

            if (stockResult.ModifiedCount == 0)
            {
                /*
                 * Esta respuesta cubre el caso en el que otro
                 * proceso modificó el inventario después de la
                 * validación inicial.
                 */
                return Conflict(
                    ApiResponse<Sale>.Fail(
                        $"El inventario de {validatedItem.Product.Name} cambió durante la operación. Intenta nuevamente"
                    )
                );
            }
        }

        var sale = new Sale
        {
            CustomerId = customer.Id,
            CustomerName = customer.FullName,
            SaleDate = DateTime.UtcNow,
            Total = total,
            Details = details,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        await _db.Sales.InsertOneAsync(sale);

        return CreatedAtAction(
            nameof(GetById),
            new { id = sale.Id },
            ApiResponse<Sale>.Ok(
                sale,
                "Venta creada e inventario actualizado correctamente"
            )
        );
    }

    // =========================================================
    // DELETE: api/Sales/{id}
    //
    // Eliminación lógica. No repone automáticamente existencias,
    // porque una eliminación administrativa no necesariamente
    // representa una devolución.
    // =========================================================
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "El identificador de la venta es obligatorio"
                )
            );
        }

        var sale = await _db.Sales
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (sale == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Venta no encontrada"
                )
            );
        }

        var update = Builders<Sale>.Update
            .Set(item => item.IsDeleted, true)
            .Set(item => item.UpdatedAt, DateTime.UtcNow);

        await _db.Sales.UpdateOneAsync(
            item =>
                item.Id == id &&
                !item.IsDeleted,
            update
        );

        return Ok(
            ApiResponse<string>.Ok(
                "Venta eliminada correctamente"
            )
        );
    }
}