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
public class ProductionController : ControllerBase
{
    private static readonly string[] WasteClassifications =
    {
        "Reusable",
        "Recyclable",
        "Sellable",
        "Rework",
        "FinalWaste"
    };

    private static readonly string[] WasteDestinations =
    {
        "Pending",
        "Reuse",
        "Sell",
        "Recycle",
        "Repair",
        "Discard"
    };

    private readonly MongoDbService _db;

    public ProductionController(MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // GET: api/Production
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orders = await _db.ProductionOrders
            .Find(order => !order.IsDeleted)
            .SortByDescending(order => order.CreatedAt)
            .ToListAsync();

        return Ok(
            ApiResponse<List<ProductionOrder>>.Ok(
                orders,
                "Órdenes de producción obtenidas correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/Production/{id}
    // =========================================================
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var order = await _db.ProductionOrders
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (order == null)
        {
            return NotFound(
                ApiResponse<ProductionOrder>.Fail(
                    "Orden de producción no encontrada"
                )
            );
        }

        return Ok(
            ApiResponse<ProductionOrder>.Ok(
                order,
                "Orden obtenida correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/Production
    // Crea la orden sin descontar inventario.
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] ProductionCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ProductId))
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "Debes seleccionar un producto"
                )
            );
        }

        if (dto.Quantity <= 0)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "La cantidad debe ser mayor a cero"
                )
            );
        }

        if (dto.Notes.Trim().Length > 1000)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "Las observaciones no pueden superar los 1000 caracteres"
                )
            );
        }

        var product = await _db.Products
            .Find(item =>
                item.Id == dto.ProductId &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "Producto no encontrado"
                )
            );
        }

        if (!product.IsActive)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "El producto está inactivo"
                )
            );
        }

        if (!product.CanBeProduced)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "El producto no está habilitado para producción"
                )
            );
        }

        var recipe = await _db.Recipes
            .Find(item =>
                item.ProductId == dto.ProductId &&
                !item.IsDeleted &&
                item.IsActive)
            .SortByDescending(item => item.Version)
            .FirstOrDefaultAsync();

        if (recipe == null)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "El producto no tiene una receta activa"
                )
            );
        }

        var materials =
            new List<ProductionMaterial>();

        foreach (var detail in recipe.Details)
        {
            var quantityWithWaste =
                detail.QuantityRequired *
                (
                    1 +
                    detail.WastePercentage / 100
                );

            var required =
                quantityWithWaste *
                dto.Quantity;

            materials.Add(
                new ProductionMaterial
                {
                    RawMaterialId =
                        detail.RawMaterialId,

                    RawMaterialCode =
                        detail.RawMaterialCode,

                    RawMaterialName =
                        detail.RawMaterialName,

                    Unit = detail.Unit,

                    QuantityPerUnit =
                        detail.QuantityRequired,

                    WastePercentage =
                        detail.WastePercentage,

                    RequiredQuantity = required,

                    IssuedQuantity = 0,

                    UnitCost =
                        detail.EstimatedUnitCost,

                    TotalCost =
                        required *
                        detail.EstimatedUnitCost
                }
            );
        }

        var now = DateTime.UtcNow;

        var folio =
            await GenerateFolioAsync(now);

        var production = new ProductionOrder
        {
            Folio = folio,

            ProductId = product.Id,
            ProductName = product.Name,

            RecipeId = recipe.Id,
            RecipeCode = recipe.Code,
            RecipeVersion = recipe.Version,

            QuantityPlanned = dto.Quantity,
            QuantityCompleted = 0,
            QuantityDefective = 0,

            Status = "Created",

            Materials = materials,

            EstimatedMaterialCost =
                materials.Sum(
                    item => item.TotalCost
                ),

            ActualMaterialCost = 0,

            Notes = dto.Notes.Trim(),

            IsDeleted = false,
            CreatedAt = now,
            CreatedBy = GetCurrentUserId()
        };

        await _db.ProductionOrders
            .InsertOneAsync(production);

        return CreatedAtAction(
            nameof(GetById),
            new { id = production.Id },
            ApiResponse<ProductionOrder>.Ok(
                production,
                "Orden de producción creada correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/Production/{id}/start
    // Descuenta la materia prima.
    // =========================================================
    [HttpPost("{id}/start")]
    public async Task<IActionResult> Start(string id)
    {
        var order = await _db.ProductionOrders
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (order == null)
        {
            return NotFound(
                ApiResponse<ProductionOrder>.Fail(
                    "Orden de producción no encontrada"
                )
            );
        }

        if (order.Status != "Created")
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "Solo pueden iniciarse órdenes en estado Created"
                )
            );
        }

        var loadedMaterials =
            new Dictionary<string, RawMaterial>();

        foreach (var requiredMaterial in order.Materials)
        {
            var material = await _db.RawMaterials
                .Find(item =>
                    item.Id ==
                        requiredMaterial.RawMaterialId &&
                    !item.IsDeleted)
                .FirstOrDefaultAsync();

            if (material == null)
            {
                return BadRequest(
                    ApiResponse<ProductionOrder>.Fail(
                        $"Materia prima no encontrada: {requiredMaterial.RawMaterialName}"
                    )
                );
            }

            if (!material.IsActive)
            {
                return BadRequest(
                    ApiResponse<ProductionOrder>.Fail(
                        $"La materia prima {material.Name} está inactiva"
                    )
                );
            }

            if (material.CurrentStock <
                requiredMaterial.RequiredQuantity)
            {
                return BadRequest(
                    ApiResponse<ProductionOrder>.Fail(
                        $"Stock insuficiente de {material.Name}. " +
                        $"Requerido: {requiredMaterial.RequiredQuantity} " +
                        $"{material.Unit}. Disponible: " +
                        $"{material.CurrentStock} {material.Unit}"
                    )
                );
            }

            loadedMaterials[material.Id] = material;
        }

        decimal actualCost = 0;

        foreach (var productionMaterial in order.Materials)
        {
            var material =
                loadedMaterials[
                    productionMaterial.RawMaterialId
                ];

            var previousStock =
                material.CurrentStock;

            var newStock =
                previousStock -
                productionMaterial.RequiredQuantity;

            productionMaterial.IssuedQuantity =
                productionMaterial.RequiredQuantity;

            productionMaterial.UnitCost =
                material.AverageCost;

            productionMaterial.TotalCost =
                productionMaterial.IssuedQuantity *
                material.AverageCost;

            actualCost +=
                productionMaterial.TotalCost;

            var update =
                Builders<RawMaterial>.Update
                    .Set(
                        item => item.CurrentStock,
                        newStock
                    )
                    .Set(
                        item => item.UpdatedAt,
                        DateTime.UtcNow
                    )
                    .Set(
                        item => item.UpdatedBy,
                        GetCurrentUserId()
                    );

            await _db.RawMaterials.UpdateOneAsync(
                item =>
                    item.Id == material.Id &&
                    !item.IsDeleted,
                update
            );

            var movement =
                new RawMaterialMovement
                {
                    RawMaterialId = material.Id,
                    RawMaterialCode = material.Code,
                    RawMaterialName = material.Name,

                    MovementType = "Production",

                    Quantity =
                        productionMaterial.RequiredQuantity,

                    PreviousStock = previousStock,
                    NewStock = newStock,

                    Unit = material.Unit,

                    Reason =
                        $"Salida para producción {order.Folio}",

                    ReferenceType = "Production",

                    ReferenceId = order.Id,

                    UnitCost = material.AverageCost,

                    TotalCost =
                        productionMaterial.TotalCost,

                    MovementDate = DateTime.UtcNow,

                    CreatedAt = DateTime.UtcNow,

                    CreatedBy = GetCurrentUserId(),

                    IsDeleted = false
                };

            await _db.RawMaterialMovements
                .InsertOneAsync(movement);
        }

        order.Status = "InProgress";
        order.StartedAt = DateTime.UtcNow;
        order.ActualMaterialCost = actualCost;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = GetCurrentUserId();

        await _db.ProductionOrders.ReplaceOneAsync(
            item =>
                item.Id == order.Id &&
                !item.IsDeleted,
            order
        );

        return Ok(
            ApiResponse<ProductionOrder>.Ok(
                order,
                "Producción iniciada y materia prima descontada correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/Production/{id}/complete
    // Aumenta producto terminado y registra merma.
    // =========================================================
    [HttpPost("{id}/complete")]
    public async Task<IActionResult> Complete(
        string id,
        [FromBody] ProductionCompleteDto dto)
    {
        var order = await _db.ProductionOrders
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (order == null)
        {
            return NotFound(
                ApiResponse<ProductionOrder>.Fail(
                    "Orden de producción no encontrada"
                )
            );
        }

        if (order.Status != "InProgress")
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "Solo pueden completarse órdenes en proceso"
                )
            );
        }

        if (dto.QuantityCompleted < 0 ||
            dto.QuantityDefective < 0)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "Las cantidades no pueden ser negativas"
                )
            );
        }

        if (
            dto.QuantityCompleted +
            dto.QuantityDefective !=
            order.QuantityPlanned
        )
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "La suma de productos terminados y defectuosos debe ser igual a la cantidad planeada"
                )
            );
        }

        var product = await _db.Products
            .Find(item =>
                item.Id == order.ProductId &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "El producto asociado ya no existe"
                )
            );
        }

        var wasteValidation =
            await ValidateProductionWastesAsync(
                order,
                dto.Wastes
            );

        if (wasteValidation != null)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    wasteValidation
                )
            );
        }

        var productUpdate =
            Builders<Product>.Update
                .Inc(
                    item => item.FinishedStock,
                    dto.QuantityCompleted
                )
                .Set(
                    item => item.UpdatedAt,
                    DateTime.UtcNow
                );

        await _db.Products.UpdateOneAsync(
            item =>
                item.Id == product.Id &&
                !item.IsDeleted,
            productUpdate
        );

        foreach (var inputWaste in dto.Wastes)
        {
            var productionMaterial =
                order.Materials.First(
                    item =>
                        item.RawMaterialId ==
                        inputWaste.RawMaterialId
                );

            var waste = new Waste
            {
                RawMaterialId =
                    productionMaterial.RawMaterialId,

                RawMaterialCode =
                    productionMaterial.RawMaterialCode,

                RawMaterialName =
                    productionMaterial.RawMaterialName,

                Unit = productionMaterial.Unit,

                ProductionOrderId = order.Id,

                ProductionFolio = order.Folio,

                QuantityGenerated =
                    inputWaste.Quantity,

                AvailableQuantity =
                    inputWaste.Quantity,

                Classification =
                    inputWaste.Classification,

                Destination =
                    inputWaste.Destination,

                Status =
                    inputWaste.Quantity > 0
                        ? "Available"
                        : "Consumed",

                UnitCost =
                    productionMaterial.UnitCost,

                EstimatedCost =
                    inputWaste.Quantity *
                    productionMaterial.UnitCost,

                EstimatedRecoveryValue =
                    inputWaste.EstimatedRecoveryValue,

                RecoveredValue = 0,

                Reason =
                    $"Merma generada en producción {order.Folio}",

                Notes = inputWaste.Notes.Trim(),

                WasteDate = DateTime.UtcNow,

                CreatedAt = DateTime.UtcNow,

                CreatedBy = GetCurrentUserId(),

                IsDeleted = false
            };

            await _db.Wastes.InsertOneAsync(waste);
        }

        order.QuantityCompleted =
            dto.QuantityCompleted;

        order.QuantityDefective =
            dto.QuantityDefective;

        order.Status = "Completed";

        order.CompletedAt = DateTime.UtcNow;

        order.Notes = string.IsNullOrWhiteSpace(
            dto.Notes)
                ? order.Notes
                : dto.Notes.Trim();

        order.UpdatedAt = DateTime.UtcNow;

        order.UpdatedBy = GetCurrentUserId();

        await _db.ProductionOrders.ReplaceOneAsync(
            item =>
                item.Id == order.Id &&
                !item.IsDeleted,
            order
        );

        return Ok(
            ApiResponse<ProductionOrder>.Ok(
                order,
                "Producción completada, producto terminado actualizado y merma registrada correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/Production/{id}/cancel
    // Solo se permite cancelar antes de iniciar.
    // =========================================================
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(
        string id,
        [FromBody] ProductionCancelDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "Debes indicar el motivo de cancelación"
                )
            );
        }

        var order = await _db.ProductionOrders
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (order == null)
        {
            return NotFound(
                ApiResponse<ProductionOrder>.Fail(
                    "Orden de producción no encontrada"
                )
            );
        }

        if (order.Status != "Created")
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "Solo pueden cancelarse órdenes que todavía no han iniciado"
                )
            );
        }

        order.Status = "Cancelled";
        order.CancelledAt = DateTime.UtcNow;
        order.Notes =
            $"{order.Notes}\nCancelación: {dto.Reason.Trim()}"
                .Trim();

        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = GetCurrentUserId();

        await _db.ProductionOrders.ReplaceOneAsync(
            item => item.Id == order.Id,
            order
        );

        return Ok(
            ApiResponse<ProductionOrder>.Ok(
                order,
                "Orden de producción cancelada correctamente"
            )
        );
    }

    private async Task<string?>
        ValidateProductionWastesAsync(
            ProductionOrder order,
            IEnumerable<ProductionWasteDto> wastes)
    {
        foreach (var waste in wastes)
        {
            if (waste.Quantity <= 0)
            {
                return "Todas las cantidades de merma deben ser mayores a cero";
            }

            if (!WasteClassifications.Contains(
                waste.Classification))
            {
                return "La clasificación de una merma no es válida";
            }

            if (!WasteDestinations.Contains(
                waste.Destination))
            {
                return "El destino de una merma no es válido";
            }

            if (waste.EstimatedRecoveryValue < 0)
            {
                return "El valor estimado de recuperación no puede ser negativo";
            }

            var productionMaterial =
                order.Materials.FirstOrDefault(
                    item =>
                        item.RawMaterialId ==
                        waste.RawMaterialId
                );

            if (productionMaterial == null)
            {
                return "La merma contiene una materia prima que no pertenece a la producción";
            }

            var totalWasteForMaterial =
                wastes
                    .Where(item =>
                        item.RawMaterialId ==
                        waste.RawMaterialId)
                    .Sum(item => item.Quantity);

            if (
                totalWasteForMaterial >
                productionMaterial.IssuedQuantity
            )
            {
                return
                    $"La merma de {productionMaterial.RawMaterialName} " +
                    "no puede ser mayor a la cantidad entregada a producción";
            }
        }

        await Task.CompletedTask;

        return null;
    }

    private async Task<string> GenerateFolioAsync(
        DateTime date)
    {
        var prefix =
            $"PRD-{date:yyyyMMdd}";

        var start = date.Date;
        var end = start.AddDays(1);

        var count =
            await _db.ProductionOrders
                .CountDocumentsAsync(
                    item =>
                        item.CreatedAt >= start &&
                        item.CreatedAt < end
                );

        var consecutive = count + 1;

        string folio;

        do
        {
            folio =
                $"{prefix}-{consecutive:0000}";

            consecutive++;
        }
        while (
            await _db.ProductionOrders
                .Find(item =>
                    item.Folio == folio)
                .AnyAsync()
        );

        return folio;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }
}