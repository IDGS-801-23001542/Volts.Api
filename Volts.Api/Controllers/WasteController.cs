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
public class WasteController : ControllerBase
{
    private static readonly string[] AllowedClassifications =
    {
        "Reusable",
        "Recyclable",
        "Sellable",
        "Rework",
        "FinalWaste"
    };

    private static readonly string[] AllowedDestinations =
    {
        "Pending",
        "Reuse",
        "Sell",
        "Recycle",
        "Repair",
        "Discard"
    };

    private static readonly string[] AllowedActions =
    {
        "Reuse",
        "Sell",
        "Recycle",
        "Repair",
        "Discard"
    };

    private readonly MongoDbService _db;

    public WasteController(MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // GET: api/Waste
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var wastes = await _db.Wastes
            .Find(waste => !waste.IsDeleted)
            .SortByDescending(waste => waste.WasteDate)
            .ToListAsync();

        return Ok(
            ApiResponse<List<Waste>>.Ok(
                wastes,
                "Registros de merma obtenidos correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/Waste/summary
    // =========================================================
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var wastes = await _db.Wastes
            .Find(waste => !waste.IsDeleted)
            .ToListAsync();

        var summary = new
        {
            totalRecords = wastes.Count,

            availableRecords =
                wastes.Count(item =>
                    item.AvailableQuantity > 0),

            estimatedWasteCost =
                wastes.Sum(item =>
                    item.EstimatedCost),

            estimatedRecoveryValue =
                wastes.Sum(item =>
                    item.EstimatedRecoveryValue),

            recoveredValue =
                wastes.Sum(item =>
                    item.RecoveredValue),

            reusableQuantity =
                wastes
                    .Where(item =>
                        item.Classification ==
                        "Reusable")
                    .Sum(item =>
                        item.AvailableQuantity),

            sellableQuantity =
                wastes
                    .Where(item =>
                        item.Classification ==
                        "Sellable")
                    .Sum(item =>
                        item.AvailableQuantity)
        };

        return Ok(
            ApiResponse<object>.Ok(
                summary,
                "Resumen de merma obtenido correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/Waste
    // Merma manual fuera de una producción.
    // Sí descuenta inventario normal.
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] WasteCreateDto dto)
    {
        var validationError =
            ValidateCreate(dto);

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<Waste>.Fail(
                    validationError
                )
            );
        }

        var material = await _db.RawMaterials
            .Find(item =>
                item.Id == dto.RawMaterialId &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (material == null)
        {
            return BadRequest(
                ApiResponse<Waste>.Fail(
                    "Materia prima no encontrada"
                )
            );
        }

        if (material.CurrentStock < dto.Quantity)
        {
            return BadRequest(
                ApiResponse<Waste>.Fail(
                    "Stock insuficiente para registrar la merma"
                )
            );
        }

        var previousStock =
            material.CurrentStock;

        var newStock =
            previousStock - dto.Quantity;

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

        var waste = new Waste
        {
            RawMaterialId = material.Id,
            RawMaterialCode = material.Code,
            RawMaterialName = material.Name,
            Unit = material.Unit,

            ProductionOrderId = null,
            ProductionFolio = null,

            QuantityGenerated = dto.Quantity,
            AvailableQuantity = dto.Quantity,

            Classification = dto.Classification,
            Destination = dto.Destination,
            Status = "Available",

            UnitCost = material.AverageCost,

            EstimatedCost =
                dto.Quantity *
                material.AverageCost,

            EstimatedRecoveryValue =
                dto.EstimatedRecoveryValue,

            RecoveredValue = 0,

            Reason = dto.Reason.Trim(),
            Notes = dto.Notes.Trim(),

            WasteDate = DateTime.UtcNow,

            CreatedAt = DateTime.UtcNow,
            CreatedBy = GetCurrentUserId(),
            IsDeleted = false
        };

        await _db.Wastes.InsertOneAsync(waste);

        var movement =
            new RawMaterialMovement
            {
                RawMaterialId = material.Id,
                RawMaterialCode = material.Code,
                RawMaterialName = material.Name,

                MovementType = "Waste",

                Quantity = dto.Quantity,

                PreviousStock = previousStock,
                NewStock = newStock,

                Unit = material.Unit,

                Reason =
                    $"Merma manual: {dto.Reason.Trim()}",

                ReferenceType = "Waste",
                ReferenceId = waste.Id,

                UnitCost = material.AverageCost,

                TotalCost =
                    dto.Quantity *
                    material.AverageCost,

                MovementDate = DateTime.UtcNow,

                CreatedAt = DateTime.UtcNow,
                CreatedBy = GetCurrentUserId(),

                IsDeleted = false
            };

        await _db.RawMaterialMovements
            .InsertOneAsync(movement);

        return Ok(
            ApiResponse<Waste>.Ok(
                waste,
                "Merma registrada y stock descontado correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/Waste/{id}/dispose
    // Reutilizar, vender, reciclar o desechar.
    // =========================================================
    [HttpPost("{id}/dispose")]
    public async Task<IActionResult> Dispose(
        string id,
        [FromBody] WasteDispositionDto dto)
    {
        if (dto.Quantity <= 0)
        {
            return BadRequest(
                ApiResponse<Waste>.Fail(
                    "La cantidad debe ser mayor a cero"
                )
            );
        }

        if (!AllowedActions.Contains(dto.Action))
        {
            return BadRequest(
                ApiResponse<Waste>.Fail(
                    "La acción seleccionada no es válida"
                )
            );
        }

        if (dto.RecoveredValue < 0)
        {
            return BadRequest(
                ApiResponse<Waste>.Fail(
                    "El valor recuperado no puede ser negativo"
                )
            );
        }

        var waste = await _db.Wastes
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (waste == null)
        {
            return NotFound(
                ApiResponse<Waste>.Fail(
                    "Registro de merma no encontrado"
                )
            );
        }

        if (waste.AvailableQuantity <
            dto.Quantity)
        {
            return BadRequest(
                ApiResponse<Waste>.Fail(
                    $"Cantidad insuficiente. Disponible: " +
                    $"{waste.AvailableQuantity} {waste.Unit}"
                )
            );
        }

        waste.AvailableQuantity -= dto.Quantity;
        waste.Destination = dto.Action;
        waste.RecoveredValue += dto.RecoveredValue;

        waste.Status =
            waste.AvailableQuantity <= 0
                ? GetCompletedStatus(dto.Action)
                : "PartiallyUsed";

        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            waste.Notes =
                $"{waste.Notes}\n{dto.Action}: {dto.Notes.Trim()}"
                    .Trim();
        }

        waste.UpdatedAt = DateTime.UtcNow;
        waste.UpdatedBy = GetCurrentUserId();

        await _db.Wastes.ReplaceOneAsync(
            item =>
                item.Id == id &&
                !item.IsDeleted,
            waste
        );

        return Ok(
            ApiResponse<Waste>.Ok(
                waste,
                "Disposición de merma registrada correctamente"
            )
        );
    }

    private static string? ValidateCreate(
        WasteCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(
            dto.RawMaterialId))
        {
            return "Debes seleccionar una materia prima";
        }

        if (dto.Quantity <= 0)
            return "La cantidad debe ser mayor a cero";

        if (!AllowedClassifications.Contains(
            dto.Classification))
        {
            return "La clasificación no es válida";
        }

        if (!AllowedDestinations.Contains(
            dto.Destination))
        {
            return "El destino no es válido";
        }

        if (string.IsNullOrWhiteSpace(dto.Reason))
            return "Debes indicar el motivo";

        if (dto.EstimatedRecoveryValue < 0)
        {
            return "El valor estimado de recuperación no puede ser negativo";
        }

        return null;
    }

    private static string GetCompletedStatus(
        string action)
    {
        return action switch
        {
            "Reuse" => "Consumed",
            "Sell" => "Sold",
            "Recycle" => "Recycled",
            "Repair" => "Reworked",
            "Discard" => "Discarded",
            _ => "Consumed"
        };
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }
}