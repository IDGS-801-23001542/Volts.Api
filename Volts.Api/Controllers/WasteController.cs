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
public class WasteController : ControllerBase
{
    private readonly MongoDbService _db;
    private readonly InventoryService _inventory;
    public WasteController(MongoDbService db, InventoryService inventory) { _db = db; _inventory = inventory; }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<List<Waste>>.Ok(
        await _db.Wastes.Find(x => !x.IsDeleted).SortByDescending(x => x.WasteDate).ToListAsync(),
        "Registros de merma obtenidos correctamente"));

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var items = await _db.Wastes.Find(x => !x.IsDeleted).ToListAsync();
        return Ok(ApiResponse<object>.Ok(new
        {
            totalRecords = items.Count,
            availableRecords = items.Count(x => x.AvailableQuantity > 0),
            estimatedWasteCost = InventoryRoundingService.RoundMoney(items.Sum(x => x.EstimatedCost)),
            estimatedRecoveryValue = InventoryRoundingService.RoundMoney(items.Sum(x => x.EstimatedRecoveryValue)),
            recoveredValue = InventoryRoundingService.RoundMoney(items.Sum(x => x.RecoveredValue)),
            reusableQuantity = items.Where(x => x.Classification == WasteClassification.Reusable).Sum(x => x.AvailableQuantity),
            sellableQuantity = items.Where(x => x.Classification == WasteClassification.Sellable).Sum(x => x.AvailableQuantity)
        }, "Resumen de merma obtenido correctamente"));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] WasteCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RawMaterialId)) return BadRequest(ApiResponse<Waste>.Fail("Selecciona una materia prima"));
        if (string.IsNullOrWhiteSpace(dto.Reason)) return BadRequest(ApiResponse<Waste>.Fail("Indica el motivo"));
        if ((dto.Notes ?? string.Empty).Trim().Length > 1000) return BadRequest(ApiResponse<Waste>.Fail("Las notas no pueden superar 1000 caracteres"));
        var material = await _db.RawMaterials.Find(x => x.Id == dto.RawMaterialId && !x.IsDeleted && x.IsActive).FirstOrDefaultAsync();
        if (material == null) return BadRequest(ApiResponse<Waste>.Fail("Materia prima inexistente o inactiva"));
        var quantityError = QuantityValidationService.ValidateQuantity(dto.Quantity, _inventory.BuildUnitSnapshot(material), "La cantidad");
        if (quantityError != null) return BadRequest(ApiResponse<Waste>.Fail(quantityError));
        var recoveryError = QuantityValidationService.ValidateCost(dto.EstimatedRecoveryValue, "El valor recuperable");
        if (recoveryError != null) return BadRequest(ApiResponse<Waste>.Fail(recoveryError));
        if (material.CurrentStock < dto.Quantity) return BadRequest(ApiResponse<Waste>.Fail("Stock insuficiente"));

        using var session = await _db.StartSessionAsync(); session.StartTransaction();
        try
        {
            var waste = new Waste
            {
                RawMaterialId = material.Id, RawMaterialCode = material.Code, RawMaterialName = material.Name,
                UnitOfMeasureId = material.UnitOfMeasureId, UnitCode = material.UnitCode, UnitName = material.UnitName,
                UnitSymbol = material.UnitSymbol, UnitAllowsDecimals = material.UnitAllowsDecimals,
                UnitDecimalPlaces = material.UnitDecimalPlaces, Unit = material.UnitSymbol,
                QuantityGenerated = dto.Quantity, AvailableQuantity = dto.Quantity,
                Classification = dto.Classification, Destination = dto.Destination, Status = WasteStatus.Available,
                UnitCost = material.AverageCost,
                EstimatedCost = InventoryRoundingService.RoundEstimatedCost(dto.Quantity * material.AverageCost),
                EstimatedRecoveryValue = InventoryRoundingService.RoundMoney(dto.EstimatedRecoveryValue),
                Reason = dto.Reason.Trim(), Notes = dto.Notes.Trim(), WasteDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow, CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier), IsDeleted = false
            };
            await _inventory.IssueRawMaterialAsync(session, material, dto.Quantity, "Waste",
                dto.Reason.Trim(), "Waste", waste.Id, User.FindFirstValue(ClaimTypes.NameIdentifier));
            await _db.Wastes.InsertOneAsync(session, waste);
            await session.CommitTransactionAsync();
            return Ok(ApiResponse<Waste>.Ok(waste, "Merma registrada correctamente"));
        }
        catch (InvalidOperationException ex) { await session.AbortTransactionAsync(); return Conflict(ApiResponse<Waste>.Fail(ex.Message)); }
        catch { await session.AbortTransactionAsync(); throw; }
    }

    [HttpPost("{id}/dispose")]
    public async Task<IActionResult> Dispose(string id, [FromBody] WasteDispositionDto dto)
    {
        if (dto.Action == WasteDestination.Pending) return BadRequest(ApiResponse<Waste>.Fail("Selecciona una acción definitiva"));
        var waste = await _db.Wastes.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (waste == null) return NotFound(ApiResponse<Waste>.Fail("Merma no encontrada"));
        var unit = new UnitOfMeasure { Symbol = waste.UnitSymbol, SingularName = waste.UnitName, AllowsDecimals = waste.UnitAllowsDecimals, DecimalPlaces = waste.UnitDecimalPlaces };
        var qError = QuantityValidationService.ValidateQuantity(dto.Quantity, unit, "La cantidad");
        if (qError != null) return BadRequest(ApiResponse<Waste>.Fail(qError));
        var valueError = QuantityValidationService.ValidateCost(dto.RecoveredValue, "El valor recuperado");
        if (valueError != null) return BadRequest(ApiResponse<Waste>.Fail(valueError));
        if (dto.Quantity > waste.AvailableQuantity) return BadRequest(ApiResponse<Waste>.Fail("La cantidad supera la merma disponible"));

        waste.AvailableQuantity -= dto.Quantity;
        waste.RecoveredValue = InventoryRoundingService.RoundMoney(waste.RecoveredValue + dto.RecoveredValue);
        waste.Destination = dto.Action;
        waste.Status = waste.AvailableQuantity == 0 ? WasteStatus.Consumed : WasteStatus.PartiallyDisposed;
        waste.Dispositions.Add(new WasteDisposition
        {
            Action = dto.Action, Quantity = dto.Quantity,
            RecoveredValue = InventoryRoundingService.RoundMoney(dto.RecoveredValue), Notes = dto.Notes.Trim(),
            DisposedAt = DateTime.UtcNow, DisposedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
        });
        waste.UpdatedAt = DateTime.UtcNow; waste.UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _db.Wastes.ReplaceOneAsync(x => x.Id == id && !x.IsDeleted, waste);
        return Ok(ApiResponse<Waste>.Ok(waste, "Disposición registrada correctamente"));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var waste = await _db.Wastes.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (waste == null) return NotFound(ApiResponse<string>.Fail("Merma no encontrada"));
        if (waste.Dispositions.Count > 0) return BadRequest(ApiResponse<string>.Fail("No puede eliminarse una merma con disposiciones"));
        if (!string.IsNullOrWhiteSpace(waste.ProductionOrderId)) return BadRequest(ApiResponse<string>.Fail("No puede eliminarse una merma generada por producción"));
        await _db.Wastes.UpdateOneAsync(x => x.Id == id, Builders<Waste>.Update.Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTime.UtcNow).Set(x => x.UpdatedBy, User.FindFirstValue(ClaimTypes.NameIdentifier)));
        return Ok(ApiResponse<string>.Ok("Merma eliminada correctamente"));
    }
}
