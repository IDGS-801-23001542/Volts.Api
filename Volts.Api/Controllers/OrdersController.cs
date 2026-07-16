using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Volts.Api.DTOs;
using Volts.Api.Models;
using Volts.Api.Models.Enums;
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
            .Find(item => !item.IsDeleted)
            .SortByDescending(item => item.CreatedAt)
            .ToListAsync();

        return Ok(
            ApiResponse<List<Order>>.Ok(
                orders,
                "Pedidos obtenidos correctamente"
            )
        );
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
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

    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> Confirm(string id)
    {
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

        if (order.Status != "PendingConfirmation")
        {
            return BadRequest(
                ApiResponse<Order>.Fail(
                    "Solo puede confirmarse un pedido pendiente"
                )
            );
        }

        var preparation = await PrepareReservation(
            order,
            createProductionOrders: true
        );

        if (!preparation.Success)
        {
            return BadRequest(
                ApiResponse<Order>.Fail(
                    preparation.Error!
                )
            );
        }

        using var session =
            await _db.StartSessionAsync();

        session.StartTransaction();

        try
        {
            foreach (var productUpdate in
                     preparation.ProductReservations)
            {
                var filter =
                    Builders<Product>.Filter.And(
                        Builders<Product>.Filter.Eq(
                            item => item.Id,
                            productUpdate.Product.Id
                        ),
                        Builders<Product>.Filter.Eq(
                            item => item.IsDeleted,
                            false
                        ),
                        Builders<Product>.Filter.Eq(
                            item => item.PhysicalStock,
                            productUpdate.Product.PhysicalStock
                        ),
                        Builders<Product>.Filter.Eq(
                            item => item.ReservedStock,
                            productUpdate.Product.ReservedStock
                        )
                    );

                var update =
                    Builders<Product>.Update
                        .Inc(
                            item => item.ReservedStock,
                            productUpdate.Quantity
                        )
                        .Set(
                            item => item.UpdatedAt,
                            DateTime.UtcNow
                        );

                var result =
                    await _db.Products.UpdateOneAsync(
                        session,
                        filter,
                        update
                    );

                if (result.ModifiedCount != 1)
                {
                    throw new InvalidOperationException(
                        $"El stock disponible de {productUpdate.Product.Name} cambió durante la reserva"
                    );
                }
            }

            foreach (var productionOrder in
                     preparation.ProductionOrders)
            {
                await _db.ProductionOrders
                    .InsertOneAsync(
                        session,
                        productionOrder
                    );
            }

            order.Details =
                preparation.Details;

            order.ProductionOrderIds =
                preparation.ProductionOrders
                    .Select(item => item.Id)
                    .ToList();

            order.Status =
                order.Details.Any(item =>
                    item.PendingQuantity > 0)
                    ? "AwaitingProduction"
                    : "ReadyForSale";

            order.ConfirmedAt =
                DateTime.UtcNow;

            if (order.Status == "ReadyForSale")
            {
                order.ReadyForSaleAt =
                    DateTime.UtcNow;
            }

            order.UpdatedAt =
                DateTime.UtcNow;

            order.UpdatedBy =
                GetCurrentUserId();

            var updateOrder =
                await _db.Orders.ReplaceOneAsync(
                    session,
                    item =>
                        item.Id == order.Id &&
                        item.Status ==
                            "PendingConfirmation",
                    order
                );

            if (updateOrder.ModifiedCount != 1)
            {
                throw new InvalidOperationException(
                    "El pedido cambió mientras se intentaba confirmar"
                );
            }

            await session.CommitTransactionAsync();
        }
        catch (InvalidOperationException exception)
        {
            await session.AbortTransactionAsync();

            return Conflict(
                ApiResponse<Order>.Fail(
                    exception.Message
                )
            );
        }
        catch
        {
            await session.AbortTransactionAsync();
            throw;
        }

        var message =
            order.Status == "ReadyForSale"
                ? "Pedido confirmado y stock reservado correctamente"
                : "Pedido confirmado; se reservaron existencias y se generaron órdenes de producción por el faltante";

        return Ok(
            ApiResponse<Order>.Ok(
                order,
                message
            )
        );
    }

    [HttpPost("{id}/synchronize-stock")]
    public async Task<IActionResult> SynchronizeStock(
        string id)
    {
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

        if (order.Status != "AwaitingProduction")
        {
            return BadRequest(
                ApiResponse<Order>.Fail(
                    "Solo se sincronizan pedidos en espera de producción"
                )
            );
        }

        var reservations =
            new List<ProductReservation>();

        foreach (var detail in order.Details)
        {
            if (detail.PendingQuantity <= 0)
            {
                continue;
            }

            var product = await _db.Products
                .Find(item =>
                    item.Id == detail.ProductId &&
                    !item.IsDeleted &&
                    item.IsActive)
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return BadRequest(
                    ApiResponse<Order>.Fail(
                        $"El producto {detail.ProductName} ya no está disponible"
                    )
                );
            }

            var available =
                Math.Max(
                    0,
                    product.PhysicalStock -
                    product.ReservedStock
                );

            var quantity =
                Math.Min(
                    available,
                    detail.PendingQuantity
                );

            if (quantity > 0)
            {
                reservations.Add(
                    new ProductReservation(
                        product,
                        quantity
                    )
                );
            }
        }

        using var session =
            await _db.StartSessionAsync();

        session.StartTransaction();

        try
        {
            foreach (var reservation in reservations)
            {
                var detail = order.Details
                    .First(item =>
                        item.ProductId ==
                        reservation.Product.Id
                    );

                var filter =
                    Builders<Product>.Filter.And(
                        Builders<Product>.Filter.Eq(
                            item => item.Id,
                            reservation.Product.Id
                        ),
                        Builders<Product>.Filter.Eq(
                            item => item.IsDeleted,
                            false
                        ),
                        Builders<Product>.Filter.Eq(
                            item => item.PhysicalStock,
                            reservation.Product.PhysicalStock
                        ),
                        Builders<Product>.Filter.Eq(
                            item => item.ReservedStock,
                            reservation.Product.ReservedStock
                        )
                    );

                var result =
                    await _db.Products.UpdateOneAsync(
                        session,
                        filter,
                        Builders<Product>.Update
                            .Inc(
                                item => item.ReservedStock,
                                reservation.Quantity
                            )
                            .Set(
                                item => item.UpdatedAt,
                                DateTime.UtcNow
                            )
                    );

                if (result.ModifiedCount != 1)
                {
                    throw new InvalidOperationException(
                        $"El stock de {reservation.Product.Name} cambió durante la sincronización"
                    );
                }

                detail.ReservedQuantity +=
                    reservation.Quantity;

                detail.PendingQuantity -=
                    reservation.Quantity;
            }

            if (order.Details.All(item =>
                    item.PendingQuantity == 0))
            {
                order.Status = "ReadyForSale";
                order.ReadyForSaleAt =
                    DateTime.UtcNow;
            }

            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = GetCurrentUserId();

            await _db.Orders.ReplaceOneAsync(
                session,
                item => item.Id == order.Id,
                order
            );

            await session.CommitTransactionAsync();
        }
        catch (InvalidOperationException exception)
        {
            await session.AbortTransactionAsync();

            return Conflict(
                ApiResponse<Order>.Fail(
                    exception.Message
                )
            );
        }
        catch
        {
            await session.AbortTransactionAsync();
            throw;
        }

        var message =
            order.Status == "ReadyForSale"
                ? "Todo el stock pendiente quedó reservado; el pedido está listo para venta"
                : "Se reservó el stock disponible; todavía existen cantidades pendientes";

        return Ok(
            ApiResponse<Order>.Ok(
                order,
                message
            )
        );
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(
        string id,
        [FromBody] OrderCancelDto dto)
    {
        var reason = dto.Reason?.Trim();

        if (string.IsNullOrWhiteSpace(reason))
        {
            return BadRequest(
                ApiResponse<Order>.Fail(
                    "Debes indicar el motivo de cancelación"
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

        if (order.Status == "Cancelled")
        {
            return BadRequest(
                ApiResponse<Order>.Fail(
                    "El pedido ya está cancelado"
                )
            );
        }

        if (order.Status == "Sold")
        {
            return BadRequest(
                ApiResponse<Order>.Fail(
                    "Un pedido vendido no puede cancelarse"
                )
            );
        }

        using var session =
            await _db.StartSessionAsync();

        session.StartTransaction();

        try
        {
            foreach (var detail in order.Details)
            {
                if (detail.ReservedQuantity <= 0)
                {
                    continue;
                }

                await _db.Products.UpdateOneAsync(
                    session,
                    item =>
                        item.Id == detail.ProductId &&
                        item.ReservedStock >=
                            detail.ReservedQuantity,
                    Builders<Product>.Update
                        .Inc(
                            item => item.ReservedStock,
                            -detail.ReservedQuantity
                        )
                        .Set(
                            item => item.UpdatedAt,
                            DateTime.UtcNow
                        )
                );

                detail.PendingQuantity =
                    detail.RequestedQuantity;

                detail.ReservedQuantity = 0;
            }

            order.Status = "Cancelled";
            order.CancelledAt = DateTime.UtcNow;
            order.CancellationReason = reason;
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = GetCurrentUserId();

            await _db.Orders.ReplaceOneAsync(
                session,
                item => item.Id == order.Id,
                order
            );

            await session.CommitTransactionAsync();
        }
        catch
        {
            await session.AbortTransactionAsync();
            throw;
        }

        return Ok(
            ApiResponse<Order>.Ok(
                order,
                "Pedido cancelado y reservas liberadas correctamente"
            )
        );
    }

    private async Task<ReservationPreparation>
        PrepareReservation(
            Order order,
            bool createProductionOrders)
    {
        var details =
            new List<OrderDetail>();

        var reservations =
            new List<ProductReservation>();

        var productionOrders =
            new List<ProductionOrder>();

        foreach (var source in order.Details)
        {
            var product = await _db.Products
                .Find(item =>
                    item.Id == source.ProductId &&
                    !item.IsDeleted &&
                    item.IsActive)
                .FirstOrDefaultAsync();

            if (product == null)
            {
                return ReservationPreparation.Fail(
                    $"El producto {source.ProductName} no existe o está inactivo"
                );
            }

            var available =
                Math.Max(
                    0,
                    product.PhysicalStock -
                    product.ReservedStock
                );

            var reserved =
                Math.Min(
                    available,
                    source.RequestedQuantity
                );

            var pending =
                source.RequestedQuantity -
                reserved;

            if (reserved > 0)
            {
                reservations.Add(
                    new ProductReservation(
                        product,
                        reserved
                    )
                );
            }

            details.Add(
                new OrderDetail
                {
                    ProductId = source.ProductId,
                    ProductName = source.ProductName,
                    RequestedQuantity =
                        source.RequestedQuantity,
                    ReservedQuantity = reserved,
                    PendingQuantity = pending,
                    UnitPrice = source.UnitPrice,
                    Subtotal = source.Subtotal
                }
            );

            if (pending <= 0 ||
                !createProductionOrders)
            {
                continue;
            }

            if (!product.CanBeProduced)
            {
                return ReservationPreparation.Fail(
                    $"Faltan {pending} unidades de {product.Name} y el producto no puede producirse"
                );
            }

            var recipe = await _db.Recipes
                .Find(item =>
                    item.ProductId == product.Id &&
                    !item.IsDeleted &&
                    item.Status == RecipeStatus.Active)
                .SortByDescending(item => item.Version)
                .FirstOrDefaultAsync();

            if (recipe == null)
            {
                return ReservationPreparation.Fail(
                    $"Faltan {pending} unidades de {product.Name} y no existe una receta activa"
                );
            }

            var productionOrder =
                await BuildProductionOrder(
                    order,
                    product,
                    recipe,
                    pending
                );

            if (!productionOrder.Success)
            {
                return ReservationPreparation.Fail(
                    productionOrder.Error!
                );
            }

            productionOrders.Add(
                productionOrder.Order!
            );
        }

        return ReservationPreparation.Ok(
            details,
            reservations,
            productionOrders
        );
    }

    private async Task<ProductionBuildResult>
        BuildProductionOrder(
            Order sourceOrder,
            Product product,
            Recipe recipe,
            int quantity)
    {
        var materials =
            new List<ProductionMaterial>();

        var shortages =
            new List<ProductionShortage>();

        foreach (var detail in recipe.Details)
        {
            var material = await _db.RawMaterials
                .Find(item =>
                    item.Id == detail.RawMaterialId &&
                    !item.IsDeleted &&
                    item.IsActive)
                .FirstOrDefaultAsync();

            if (material == null)
            {
                return ProductionBuildResult.Fail(
                    $"El material {detail.RawMaterialName} de la receta no está disponible"
                );
            }

            var requiredQuantity = decimal.Round(
                detail.TotalQuantityPerUnit *
                quantity,
                material.UnitDecimalPlaces,
                MidpointRounding.AwayFromZero
            );

            materials.Add(
                new ProductionMaterial
                {
                    RawMaterialId = material.Id,
                    RawMaterialCode = material.Code,
                    RawMaterialName = material.Name,
                    UnitOfMeasureId =
                        material.UnitOfMeasureId,
                    UnitCode = material.UnitCode,
                    UnitName = material.UnitName,
                    UnitSymbol = material.UnitSymbol,
                    UnitAllowsDecimals =
                        material.UnitAllowsDecimals,
                    UnitDecimalPlaces =
                        material.UnitDecimalPlaces,
                    Unit = material.UnitSymbol,
                    QuantityPerUnit =
                        detail.QuantityRequired,
                    WastePercentage =
                        detail.WastePercentage,
                    RequiredQuantity =
                        requiredQuantity,
                    UnitCost =
                        material.AverageCost,
                    TotalCost =
                        InventoryRoundingService
                            .RoundEstimatedCost(
                                requiredQuantity *
                                material.AverageCost
                            )
                }
            );

            if (material.CurrentStock <
                requiredQuantity)
            {
                shortages.Add(
                    new ProductionShortage
                    {
                        RawMaterialId = material.Id,
                        RawMaterialName =
                            material.Name,
                        UnitSymbol =
                            material.UnitSymbol,
                        RequiredQuantity =
                            requiredQuantity,
                        AvailableQuantity =
                            material.CurrentStock,
                        MissingQuantity =
                            requiredQuantity -
                            material.CurrentStock
                    }
                );
            }
        }

        var now = DateTime.UtcNow;

        return ProductionBuildResult.Ok(
            new ProductionOrder
            {
                Folio =
                    $"PRD-{now:yyyyMMdd-HHmmss}-" +
                    Guid.NewGuid()
                        .ToString("N")[..6]
                        .ToUpperInvariant(),
                ProductId = product.Id,
                ProductName = product.Name,
                RecipeId = recipe.Id,
                RecipeCode = recipe.Code,
                RecipeVersion = recipe.Version,
                QuantityPlanned = quantity,
                Status = ProductionStatus.Created,
                Materials = materials,
                Shortages = shortages,
                HasShortages =
                    shortages.Count > 0,
                EstimatedMaterialCost =
                    InventoryRoundingService
                        .RoundEstimatedCost(
                            materials.Sum(
                                item => item.TotalCost
                            )
                        ),
                SourceOrderId =
                    sourceOrder.Id,
                Notes =
                    $"Producción generada automáticamente por el pedido {sourceOrder.Folio}.",
                IsDeleted = false,
                CreatedAt = now,
                CreatedBy = GetCurrentUserId()
            }
        );
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }

    private sealed record ProductReservation(
        Product Product,
        int Quantity
    );

    private sealed class ReservationPreparation
    {
        public bool Success { get; private init; }
        public string? Error { get; private init; }
        public List<OrderDetail> Details { get; private init; } = new();
        public List<ProductReservation> ProductReservations { get; private init; } = new();
        public List<ProductionOrder> ProductionOrders { get; private init; } = new();

        public static ReservationPreparation Ok(
            List<OrderDetail> details,
            List<ProductReservation> reservations,
            List<ProductionOrder> productionOrders)
        {
            return new ReservationPreparation
            {
                Success = true,
                Details = details,
                ProductReservations = reservations,
                ProductionOrders = productionOrders
            };
        }

        public static ReservationPreparation Fail(
            string error)
        {
            return new ReservationPreparation
            {
                Success = false,
                Error = error
            };
        }
    }

    private sealed class ProductionBuildResult
    {
        public bool Success { get; private init; }
        public string? Error { get; private init; }
        public ProductionOrder? Order { get; private init; }

        public static ProductionBuildResult Ok(
            ProductionOrder order)
        {
            return new ProductionBuildResult
            {
                Success = true,
                Order = order
            };
        }

        public static ProductionBuildResult Fail(
            string error)
        {
            return new ProductionBuildResult
            {
                Success = false,
                Error = error
            };
        }
    }
}
