using System.Security.Claims;
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
            .Find(item => !item.IsDeleted)
            .SortByDescending(item => item.SaleDate)
            .ToListAsync();

        return Ok(
            ApiResponse<List<Sale>>.Ok(
                sales,
                "Ventas obtenidas correctamente"
            )
        );
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
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

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaleCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.OrderId))
        {
            return BadRequest(
                ApiResponse<Sale>.Fail(
                    "Debes seleccionar un pedido"
                )
            );
        }

        var order = await _db.Orders
            .Find(item =>
                item.Id == dto.OrderId &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (order == null)
        {
            return NotFound(
                ApiResponse<Sale>.Fail(
                    "Pedido no encontrado"
                )
            );
        }

        if (order.Status != "ReadyForSale")
        {
            return BadRequest(
                ApiResponse<Sale>.Fail(
                    "Solo puede venderse un pedido listo para venta"
                )
            );
        }

        if (order.Details.Any(item =>
                item.PendingQuantity != 0 ||
                item.ReservedQuantity !=
                    item.RequestedQuantity))
        {
            return BadRequest(
                ApiResponse<Sale>.Fail(
                    "El pedido no tiene todas sus unidades reservadas"
                )
            );
        }

        var existingSale = await _db.Sales
            .Find(item =>
                item.OrderId == order.Id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (existingSale != null)
        {
            return Conflict(
                ApiResponse<Sale>.Fail(
                    "El pedido ya tiene una venta asociada"
                )
            );
        }

        var plan = await _db.CommercialPlans
            .Find(item =>
                item.Id == order.CommercialPlanId &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (plan == null)
        {
            return BadRequest(
                ApiResponse<Sale>.Fail(
                    "El plan comercial relacionado ya no existe"
                )
            );
        }

        var now = DateTime.UtcNow;

        var sale = new Sale
        {
            Folio = BuildFolio("SAL", now),
            OrderId = order.Id,
            OrderFolio = order.Folio,
            QuoteId = order.QuoteId,
            QuoteFolio = order.QuoteFolio,
            RecipientType = order.RecipientType,
            CustomerId = order.CustomerId,
            InstitutionId = order.InstitutionId,
            RecipientName = order.RecipientName,
            ContactName = order.ContactName,
            Email = order.Email,
            Phone = order.Phone,
            CommercialPlanId = order.CommercialPlanId,
            CommercialPlanName = order.CommercialPlanName,
            CommercialPackageId = order.CommercialPackageId,
            CommercialPackageName = order.CommercialPackageName,
            SaleDate = now,
            Subtotal = order.Subtotal,
            Discount = order.Discount,
            Tax = order.Tax,
            Shipping = order.Shipping,
            Total = order.Total,
            Details = order.Details.Select(item =>
                new SaleDetail
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.RequestedQuantity,
                    UnitPrice = item.UnitPrice,
                    Subtotal = item.Subtotal
                }).ToList(),
            IsDeleted = false,
            CreatedAt = now,
            CreatedBy = GetCurrentUserId()
        };

        var licenses = new List<License>();

        foreach (var detail in sale.Details)
        {
            for (var index = 0; index < detail.Quantity; index++)
            {
                licenses.Add(
                    new License
                    {
                        LicenseCode =
                            GenerateLicenseCode(),
                        SaleId = sale.Id,
                        SaleFolio = sale.Folio,
                        OrderId = order.Id,
                        OrderFolio = order.Folio,
                        SaleDetailId = detail.Id,
                        ProductId = detail.ProductId,
                        ProductName = detail.ProductName,
                        CommercialPlanId =
                            order.CommercialPlanId,
                        CommercialPlanName =
                            order.CommercialPlanName,
                        CommercialPackageId =
                            order.CommercialPackageId,
                        CommercialPackageName =
                            order.CommercialPackageName,
                        RecipientType =
                            order.RecipientType,
                        CustomerId = order.CustomerId,
                        InstitutionId =
                            order.InstitutionId,
                        RecipientName =
                            order.RecipientName,
                        Status = "Available",
                        WarrantyStartDate = now,
                        WarrantyEndDate =
                            now.AddMonths(
                                plan.WarrantyMonths
                            ),
                        IsDeleted = false,
                        CreatedAt = now,
                        CreatedBy =
                            GetCurrentUserId()
                    }
                );
            }
        }

        sale.LicenseIds =
            licenses.Select(item => item.Id).ToList();

        using var session =
            await _db.StartSessionAsync();

        session.StartTransaction();

        try
        {
            foreach (var detail in order.Details)
            {
                var product = await _db.Products
                    .Find(
                        session,
                        item =>
                            item.Id == detail.ProductId &&
                            !item.IsDeleted)
                    .FirstOrDefaultAsync();

                if (product == null)
                {
                    throw new InvalidOperationException(
                        $"Producto no encontrado: {detail.ProductName}"
                    );
                }

                var result = await _db.Products
                    .UpdateOneAsync(
                        session,
                        item =>
                            item.Id == product.Id &&
                            item.PhysicalStock ==
                                product.PhysicalStock &&
                            item.ReservedStock ==
                                product.ReservedStock &&
                            item.PhysicalStock >=
                                detail.RequestedQuantity &&
                            item.ReservedStock >=
                                detail.RequestedQuantity,
                        Builders<Product>.Update
                            .Inc(
                                item =>
                                    item.PhysicalStock,
                                -detail.RequestedQuantity
                            )
                            .Inc(
                                item =>
                                    item.ReservedStock,
                                -detail.RequestedQuantity
                            )
                            .Set(
                                item => item.UpdatedAt,
                                now
                            )
                            .Set(
                                item => item.UpdatedBy,
                                GetCurrentUserId()
                            )
                    );

                if (result.ModifiedCount != 1)
                {
                    throw new InvalidOperationException(
                        $"El inventario de {product.Name} cambió durante la venta"
                    );
                }
            }

            await _db.Sales.InsertOneAsync(
                session,
                sale
            );

            if (licenses.Count > 0)
            {
                await _db.Licenses.InsertManyAsync(
                    session,
                    licenses
                );
            }

            order.Status = "Sold";
            order.UpdatedAt = now;
            order.UpdatedBy = GetCurrentUserId();

            var orderResult =
                await _db.Orders.ReplaceOneAsync(
                    session,
                    item =>
                        item.Id == order.Id &&
                        item.Status == "ReadyForSale",
                    order
                );

            if (orderResult.ModifiedCount != 1)
            {
                throw new InvalidOperationException(
                    "El pedido cambió durante la venta"
                );
            }

            await session.CommitTransactionAsync();
        }
        catch (InvalidOperationException exception)
        {
            await session.AbortTransactionAsync();

            return Conflict(
                ApiResponse<Sale>.Fail(
                    exception.Message
                )
            );
        }
        catch
        {
            await session.AbortTransactionAsync();
            throw;
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = sale.Id },
            ApiResponse<Sale>.Ok(
                sale,
                $"Venta creada correctamente. Se generaron {licenses.Count} licencias"
            )
        );
    }

    private static string GenerateLicenseCode()
    {
        return
            $"VOLTS-{DateTime.UtcNow:yyyyMMdd}-" +
            Guid.NewGuid()
                .ToString("N")[..10]
                .ToUpperInvariant();
    }

    private static string BuildFolio(
        string prefix,
        DateTime now)
    {
        return
            $"{prefix}-{now:yyyyMMdd-HHmmss}-" +
            Guid.NewGuid()
                .ToString("N")[..6]
                .ToUpperInvariant();
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }
}
