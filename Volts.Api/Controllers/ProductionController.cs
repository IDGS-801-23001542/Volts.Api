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
public class ProductionController : ControllerBase
{
    private readonly MongoDbService _db;
    private readonly InventoryService _inventory;

    public ProductionController(
        MongoDbService db,
        InventoryService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

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

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "El identificador de la orden es obligatorio"
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
                    "Orden no encontrada"
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

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] ProductionCreateDto dto)
    {
        var quantityError =
            QuantityValidationService
                .ValidateWholeQuantity(
                    dto.Quantity,
                    "La cantidad"
                );

        if (quantityError != null)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    quantityError
                )
            );
        }

        if (string.IsNullOrWhiteSpace(dto.ProductId))
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "Debes seleccionar un producto"
                )
            );
        }

        var notes = dto.Notes?.Trim() ?? string.Empty;

        if (notes.Length > 1000)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "Las notas no pueden superar 1000 caracteres"
                )
            );
        }

        var product = await _db.Products
            .Find(item =>
                item.Id == dto.ProductId &&
                !item.IsDeleted &&
                item.IsActive)
            .FirstOrDefaultAsync();

        if (product == null || !product.CanBeProduced)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "Producto inexistente, inactivo o no producible"
                )
            );
        }

        var recipe = await _db.Recipes
            .Find(item =>
                item.ProductId == dto.ProductId &&
                !item.IsDeleted &&
                item.Status == RecipeStatus.Active)
            .SortByDescending(item => item.Version)
            .FirstOrDefaultAsync();

        if (recipe == null)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "El producto no tiene receta activa"
                )
            );
        }

        var productionMaterials =
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
                return BadRequest(
                    ApiResponse<ProductionOrder>.Fail(
                        $"El material {detail.RawMaterialName} no está disponible"
                    )
                );
            }

            var requiredQuantity = decimal.Round(
                detail.TotalQuantityPerUnit * dto.Quantity,
                material.UnitDecimalPlaces,
                MidpointRounding.AwayFromZero
            );

            if (!material.UnitAllowsDecimals &&
                decimal.Truncate(requiredQuantity) !=
                requiredQuantity)
            {
                return BadRequest(
                    ApiResponse<ProductionOrder>.Fail(
                        $"La receta genera una cantidad fraccionaria inválida de {material.Name}"
                    )
                );
            }

            productionMaterials.Add(
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
                    RequiredQuantity = requiredQuantity,
                    UnitCost = material.AverageCost,
                    TotalCost =
                        InventoryRoundingService
                            .RoundEstimatedCost(
                                requiredQuantity *
                                material.AverageCost
                            )
                }
            );

            if (material.CurrentStock < requiredQuantity)
            {
                shortages.Add(
                    new ProductionShortage
                    {
                        RawMaterialId = material.Id,
                        RawMaterialName = material.Name,
                        UnitSymbol = material.UnitSymbol,
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

        var order = new ProductionOrder
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
            QuantityPlanned = dto.Quantity,
            Status = ProductionStatus.Created,
            Materials = productionMaterials,
            Shortages = shortages,
            HasShortages = shortages.Count > 0,

            EstimatedMaterialCost =
                InventoryRoundingService
                    .RoundEstimatedCost(
                        productionMaterials.Sum(
                            item => item.TotalCost
                        )
                    ),

            SourceOrderId =
                string.IsNullOrWhiteSpace(
                    dto.SourceOrderId)
                    ? null
                    : dto.SourceOrderId.Trim(),

            Notes = notes,
            IsDeleted = false,
            CreatedAt = now,
            CreatedBy = GetCurrentUserId()
        };

        await _db.ProductionOrders
            .InsertOneAsync(order);

        var message = order.HasShortages
            ? "Orden creada con faltantes de material"
            : "Orden creada y lista para iniciar";

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                id = order.Id
            },
            ApiResponse<ProductionOrder>.Ok(
                order,
                message
            )
        );
    }

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
                    "Orden no encontrada"
                )
            );
        }

        if (order.Status != ProductionStatus.Created)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "Solo pueden iniciarse órdenes en estado Created"
                )
            );
        }

        var loadedMaterials =
            new Dictionary<string, RawMaterial>();

        var shortages =
            new List<ProductionShortage>();

        foreach (var line in order.Materials)
        {
            var material = await _db.RawMaterials
                .Find(item =>
                    item.Id == line.RawMaterialId &&
                    !item.IsDeleted &&
                    item.IsActive)
                .FirstOrDefaultAsync();

            if (material == null)
            {
                return BadRequest(
                    ApiResponse<ProductionOrder>.Fail(
                        $"Material no disponible: {line.RawMaterialName}"
                    )
                );
            }

            loadedMaterials[material.Id] = material;

            if (material.CurrentStock <
                line.RequiredQuantity)
            {
                shortages.Add(
                    new ProductionShortage
                    {
                        RawMaterialId = material.Id,
                        RawMaterialName = material.Name,
                        UnitSymbol = material.UnitSymbol,
                        RequiredQuantity =
                            line.RequiredQuantity,
                        AvailableQuantity =
                            material.CurrentStock,
                        MissingQuantity =
                            line.RequiredQuantity -
                            material.CurrentStock
                    }
                );
            }
        }

        if (shortages.Count > 0)
        {
            order.Shortages = shortages;
            order.HasShortages = true;
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = GetCurrentUserId();

            await _db.ProductionOrders
                .ReplaceOneAsync(
                    item => item.Id == order.Id,
                    order
                );

            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "No hay stock suficiente para iniciar la orden"
                )
            );
        }

        using var session =
            await _db.StartSessionAsync();

        session.StartTransaction();

        try
        {
            decimal actualCost = 0;

            foreach (var line in order.Materials)
            {
                var material =
                    loadedMaterials[line.RawMaterialId];

                line.IssuedQuantity =
                    line.RequiredQuantity;

                line.UnitCost =
                    material.AverageCost;

                line.TotalCost =
                    InventoryRoundingService
                        .RoundEstimatedCost(
                            line.IssuedQuantity *
                            line.UnitCost
                        );

                actualCost += line.TotalCost;

                await _inventory.IssueRawMaterialAsync(
                    session,
                    material,
                    line.IssuedQuantity,
                    "ProductionIssue",
                    $"Salida para producción {order.Folio}",
                    "Production",
                    order.Id,
                    GetCurrentUserId()
                );
            }

            order.Status =
                ProductionStatus.InProgress;

            order.StartedAt =
                DateTime.UtcNow;

            order.HasShortages = false;
            order.Shortages.Clear();

            order.ActualMaterialCost =
                InventoryRoundingService
                    .RoundEstimatedCost(actualCost);

            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = GetCurrentUserId();

            var updateResult =
                await _db.ProductionOrders
                    .ReplaceOneAsync(
                        session,
                        item =>
                            item.Id == order.Id &&
                            item.Status ==
                                ProductionStatus.Created,
                        order
                    );

            if (updateResult.ModifiedCount != 1)
            {
                throw new InvalidOperationException(
                    "La orden cambió mientras se intentaba iniciar"
                );
            }

            await session.CommitTransactionAsync();

            return Ok(
                ApiResponse<ProductionOrder>.Ok(
                    order,
                    "Producción iniciada e inventario descontado correctamente"
                )
            );
        }
        catch (InvalidOperationException exception)
        {
            await session.AbortTransactionAsync();

            return Conflict(
                ApiResponse<ProductionOrder>.Fail(
                    exception.Message
                )
            );
        }
        catch
        {
            await session.AbortTransactionAsync();
            throw;
        }
    }

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
                    "Orden no encontrada"
                )
            );
        }

        if (order.Status !=
            ProductionStatus.InProgress)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "Solo pueden completarse órdenes en proceso"
                )
            );
        }

        var goodQuantityError =
            QuantityValidationService
                .ValidateWholeQuantity(
                    dto.QuantityCompleted,
                    "Las unidades buenas",
                    positiveRequired: false
                );

        if (goodQuantityError != null)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    goodQuantityError
                )
            );
        }

        var defectiveQuantityError =
            QuantityValidationService
                .ValidateWholeQuantity(
                    dto.QuantityDefective,
                    "Las unidades defectuosas",
                    positiveRequired: false
                );

        if (defectiveQuantityError != null)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    defectiveQuantityError
                )
            );
        }

        if (dto.QuantityCompleted +
            dto.QuantityDefective !=
            order.QuantityPlanned)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "Buenas + defectuosas debe ser igual a la cantidad planeada"
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
                    "El producto asociado no existe"
                )
            );
        }

        var wasteInputs =
            dto.Wastes ?? new List<ProductionWasteDto>();

        var wasteError =
            ValidateWastes(
                order,
                wasteInputs
            );

        if (wasteError != null)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    wasteError
                )
            );
        }

        using var session =
            await _db.StartSessionAsync();

        session.StartTransaction();

        try
        {
            await _inventory.AddFinishedProductAsync(
                session,
                product,
                dto.QuantityCompleted,
                GetCurrentUserId()
            );

            foreach (var input in wasteInputs)
            {
                var line = order.Materials
                    .First(item =>
                        item.RawMaterialId ==
                        input.RawMaterialId
                    );

                var wasteNotes =
                    input.Notes?.Trim() ?? string.Empty;

                var waste = new Waste
                {
                    RawMaterialId = line.RawMaterialId,
                    RawMaterialCode = line.RawMaterialCode,
                    RawMaterialName = line.RawMaterialName,
                    UnitOfMeasureId = line.UnitOfMeasureId,
                    UnitCode = line.UnitCode,
                    UnitName = line.UnitName,
                    UnitSymbol = line.UnitSymbol,
                    UnitAllowsDecimals =
                        line.UnitAllowsDecimals,
                    UnitDecimalPlaces =
                        line.UnitDecimalPlaces,
                    Unit = line.UnitSymbol,
                    ProductionOrderId = order.Id,
                    ProductionFolio = order.Folio,
                    QuantityGenerated = input.Quantity,
                    AvailableQuantity = input.Quantity,
                    Classification = input.Classification,
                    Destination = input.Destination,
                    Status = WasteStatus.Available,
                    UnitCost = line.UnitCost,

                    EstimatedCost =
                        InventoryRoundingService
                            .RoundEstimatedCost(
                                input.Quantity *
                                line.UnitCost
                            ),

                    EstimatedRecoveryValue =
                        InventoryRoundingService
                            .RoundMoney(
                                input.EstimatedRecoveryValue
                            ),

                    Reason =
                        $"Merma generada en {order.Folio}",

                    Notes = wasteNotes,
                    WasteDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = GetCurrentUserId(),
                    IsDeleted = false
                };

                await _db.Wastes.InsertOneAsync(
                    session,
                    waste
                );
            }

            order.QuantityCompleted =
                dto.QuantityCompleted;

            order.QuantityDefective =
                dto.QuantityDefective;

            order.Status =
                ProductionStatus.Completed;

            order.CompletedAt =
                DateTime.UtcNow;

            var completionNotes =
                dto.Notes?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(
                    completionNotes))
            {
                order.Notes = completionNotes;
            }

            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = GetCurrentUserId();

            var updateResult =
                await _db.ProductionOrders
                    .ReplaceOneAsync(
                        session,
                        item =>
                            item.Id == order.Id &&
                            item.Status ==
                                ProductionStatus.InProgress,
                        order
                    );

            if (updateResult.ModifiedCount != 1)
            {
                throw new InvalidOperationException(
                    "La orden cambió mientras se intentaba completar"
                );
            }

            await session.CommitTransactionAsync();

            return Ok(
                ApiResponse<ProductionOrder>.Ok(
                    order,
                    "Producción completada correctamente"
                )
            );
        }
        catch (InvalidOperationException exception)
        {
            await session.AbortTransactionAsync();

            return Conflict(
                ApiResponse<ProductionOrder>.Fail(
                    exception.Message
                )
            );
        }
        catch
        {
            await session.AbortTransactionAsync();
            throw;
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(
        string id,
        [FromBody] ProductionCancelDto dto)
    {
        var reason =
            dto.Reason?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(reason))
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "Indica el motivo"
                )
            );
        }

        if (reason.Length > 500)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "El motivo no puede superar 500 caracteres"
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
                    "Orden no encontrada"
                )
            );
        }

        if (order.Status != ProductionStatus.Created)
        {
            return BadRequest(
                ApiResponse<ProductionOrder>.Fail(
                    "Solo puede cancelarse antes de iniciar"
                )
            );
        }

        order.Status = ProductionStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;

        var cancellationNote =
            $"Cancelación: {reason}";

        order.Notes =
            string.IsNullOrWhiteSpace(order.Notes)
                ? cancellationNote
                : $"{order.Notes}{Environment.NewLine}{cancellationNote}";

        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = GetCurrentUserId();

        var result = await _db.ProductionOrders
            .ReplaceOneAsync(
                item =>
                    item.Id == order.Id &&
                    item.Status ==
                        ProductionStatus.Created,
                order
            );

        if (result.ModifiedCount != 1)
        {
            return Conflict(
                ApiResponse<ProductionOrder>.Fail(
                    "La orden cambió mientras se intentaba cancelar"
                )
            );
        }

        return Ok(
            ApiResponse<ProductionOrder>.Ok(
                order,
                "Orden cancelada correctamente"
            )
        );
    }

    private static string? ValidateWastes(
        ProductionOrder order,
        IEnumerable<ProductionWasteDto> inputs)
    {
        foreach (var group in inputs.GroupBy(
                     item => item.RawMaterialId))
        {
            if (string.IsNullOrWhiteSpace(group.Key))
            {
                return "Todas las mermas deben indicar una materia prima";
            }

            var line = order.Materials
                .FirstOrDefault(item =>
                    item.RawMaterialId == group.Key
                );

            if (line == null)
            {
                return "La merma contiene un material ajeno a la orden";
            }

            var unit = new UnitOfMeasure
            {
                Symbol = line.UnitSymbol,
                SingularName = line.UnitName,
                AllowsDecimals =
                    line.UnitAllowsDecimals,
                DecimalPlaces =
                    line.UnitDecimalPlaces
            };

            foreach (var input in group)
            {
                var quantityError =
                    QuantityValidationService
                        .ValidateQuantity(
                            input.Quantity,
                            unit,
                            $"La merma de {line.RawMaterialName}"
                        );

                if (quantityError != null)
                {
                    return quantityError;
                }

                var recoveryError =
                    QuantityValidationService
                        .ValidateCost(
                            input.EstimatedRecoveryValue,
                            "El valor recuperable"
                        );

                if (recoveryError != null)
                {
                    return recoveryError;
                }

                var notes =
                    input.Notes?.Trim() ?? string.Empty;

                if (notes.Length > 500)
                {
                    return "Las notas de merma no pueden superar 500 caracteres";
                }
            }

            if (group.Sum(item => item.Quantity) >
                line.IssuedQuantity)
            {
                return
                    $"La merma de {line.RawMaterialName} supera lo entregado a producción";
            }
        }

        return null;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }
}
