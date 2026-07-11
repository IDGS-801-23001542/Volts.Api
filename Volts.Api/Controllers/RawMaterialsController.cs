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
public class RawMaterialsController : ControllerBase
{
    private static readonly string[] AllowedCategories =
    {
        "Cardboard",
        "Electronics",
        "Mechanical",
        "Textiles",
        "Adhesives",
        "Consumables",
        "Soldering",
        "Packaging",
        "Other"
    };

    private static readonly string[] AllowedMovementTypes =
    {
        "Entry",
        "Exit",
        "Adjustment"
    };

    private readonly MongoDbService _db;

    public RawMaterialsController(MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // GET: api/RawMaterials
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var materials = await _db.RawMaterials
            .Find(material => !material.IsDeleted)
            .SortBy(material => material.Name)
            .ToListAsync();

        return Ok(
            ApiResponse<List<RawMaterial>>.Ok(
                materials,
                "Materias primas obtenidas correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/RawMaterials/{id}
    // =========================================================
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var material = await _db.RawMaterials
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (material == null)
        {
            return NotFound(
                ApiResponse<RawMaterial>.Fail(
                    "Materia prima no encontrada"
                )
            );
        }

        return Ok(
            ApiResponse<RawMaterial>.Ok(
                material,
                "Materia prima obtenida correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/RawMaterials/summary
    // =========================================================
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var materials = await _db.RawMaterials
            .Find(material => !material.IsDeleted)
            .ToListAsync();

        var totalMaterials = materials.Count;

        var activeMaterials = materials.Count(
            material => material.IsActive
        );

        var lowStockMaterials = materials.Count(
            material =>
                material.IsActive &&
                material.CurrentStock <= material.MinimumStock
        );

        var outOfStockMaterials = materials.Count(
            material =>
                material.IsActive &&
                material.CurrentStock <= 0
        );

        var recycledMaterials = materials.Count(
            material => material.IsRecycled
        );

        var reusableMaterials = materials.Count(
            material => material.IsReusable
        );

        var totalInventoryValue = materials.Sum(
            material =>
                material.CurrentStock *
                material.AverageCost
        );

        var data = new
        {
            totalMaterials,
            activeMaterials,
            lowStockMaterials,
            outOfStockMaterials,
            recycledMaterials,
            reusableMaterials,
            totalInventoryValue
        };

        return Ok(
            ApiResponse<object>.Ok(
                data,
                "Resumen de materia prima obtenido correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/RawMaterials/low-stock
    // =========================================================
    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock()
    {
        var materials = await _db.RawMaterials
            .Find(material =>
                !material.IsDeleted &&
                material.IsActive &&
                material.CurrentStock <= material.MinimumStock)
            .SortBy(material => material.Name)
            .ToListAsync();

        return Ok(
            ApiResponse<List<RawMaterial>>.Ok(
                materials,
                "Materiales con stock bajo obtenidos correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/RawMaterials/{id}/movements
    // =========================================================
    [HttpGet("{id}/movements")]
    public async Task<IActionResult> GetMovements(string id)
    {
        var materialExists = await _db.RawMaterials
            .Find(material =>
                material.Id == id &&
                !material.IsDeleted)
            .AnyAsync();

        if (!materialExists)
        {
            return NotFound(
                ApiResponse<List<RawMaterialMovement>>.Fail(
                    "Materia prima no encontrada"
                )
            );
        }

        var movements = await _db.RawMaterialMovements
            .Find(movement =>
                movement.RawMaterialId == id &&
                !movement.IsDeleted)
            .SortByDescending(movement =>
                movement.MovementDate)
            .Limit(200)
            .ToListAsync();

        return Ok(
            ApiResponse<List<RawMaterialMovement>>.Ok(
                movements,
                "Movimientos obtenidos correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/RawMaterials
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] RawMaterialCreateDto dto)
    {
        var validationError = ValidateMaterial(dto);

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    validationError
                )
            );
        }

        var normalizedCode = NormalizeCode(dto.Code);
        var normalizedName = dto.Name.Trim();

        var codeExists = await _db.RawMaterials
            .Find(material =>
                material.Code.ToUpper() ==
                normalizedCode.ToUpper() &&
                !material.IsDeleted)
            .AnyAsync();

        if (codeExists)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    "Ya existe una materia prima con ese código"
                )
            );
        }

        var nameExists = await _db.RawMaterials
            .Find(material =>
                material.Name.ToLower() ==
                normalizedName.ToLower() &&
                !material.IsDeleted)
            .AnyAsync();

        if (nameExists)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    "Ya existe una materia prima con ese nombre"
                )
            );
        }

        var material = new RawMaterial
        {
            Code = normalizedCode,
            Name = normalizedName,
            Description = dto.Description.Trim(),
            Category = dto.Category.Trim(),
            Unit = dto.Unit.Trim(),
            CurrentStock = dto.CurrentStock,
            MinimumStock = dto.MinimumStock,
            MaximumStock = dto.MaximumStock,
            AverageCost = dto.AverageCost,
            LastPurchaseCost = dto.LastPurchaseCost,
            IsRecycled = dto.IsRecycled,
            IsReusable = dto.IsReusable,
            RequiresPurchase = dto.RequiresPurchase,
            StorageLocation =
                dto.StorageLocation.Trim(),
            PreferredSupplierId =
                NormalizeOptional(dto.PreferredSupplierId),
            PreferredSupplierName =
                NormalizeOptional(dto.PreferredSupplierName),
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = GetCurrentUserId()
        };

        await _db.RawMaterials.InsertOneAsync(material);

        if (material.CurrentStock > 0)
        {
            await RegisterMovementAsync(
                material,
                movementType: "Entry",
                quantity: material.CurrentStock,
                previousStock: 0,
                newStock: material.CurrentStock,
                reason: "Inventario inicial",
                unitCost: material.AverageCost,
                referenceType: "InitialStock",
                referenceId: null
            );
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = material.Id },
            ApiResponse<RawMaterial>.Ok(
                material,
                "Materia prima creada correctamente"
            )
        );
    }

    // =========================================================
    // PUT: api/RawMaterials/{id}
    // Los cambios de stock se hacen mediante movimientos.
    // =========================================================
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] RawMaterialUpdateDto dto)
    {
        var validationError = ValidateMaterial(dto);

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    validationError
                )
            );
        }

        var material = await _db.RawMaterials
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (material == null)
        {
            return NotFound(
                ApiResponse<RawMaterial>.Fail(
                    "Materia prima no encontrada"
                )
            );
        }

        var normalizedCode = NormalizeCode(dto.Code);
        var normalizedName = dto.Name.Trim();

        var duplicateCode = await _db.RawMaterials
            .Find(item =>
                item.Id != id &&
                item.Code.ToUpper() ==
                normalizedCode.ToUpper() &&
                !item.IsDeleted)
            .AnyAsync();

        if (duplicateCode)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    "Ya existe otra materia prima con ese código"
                )
            );
        }

        var duplicateName = await _db.RawMaterials
            .Find(item =>
                item.Id != id &&
                item.Name.ToLower() ==
                normalizedName.ToLower() &&
                !item.IsDeleted)
            .AnyAsync();

        if (duplicateName)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    "Ya existe otra materia prima con ese nombre"
                )
            );
        }

        material.Code = normalizedCode;
        material.Name = normalizedName;
        material.Description =
            dto.Description.Trim();
        material.Category =
            dto.Category.Trim();
        material.Unit =
            dto.Unit.Trim();

        /*
         * El stock actual no se reemplaza desde la edición.
         * Las entradas y salidas deben registrarse con
         * el endpoint adjust-stock.
         */
        material.MinimumStock =
            dto.MinimumStock;
        material.MaximumStock =
            dto.MaximumStock;
        material.AverageCost =
            dto.AverageCost;
        material.LastPurchaseCost =
            dto.LastPurchaseCost;
        material.IsRecycled =
            dto.IsRecycled;
        material.IsReusable =
            dto.IsReusable;
        material.RequiresPurchase =
            dto.RequiresPurchase;
        material.StorageLocation =
            dto.StorageLocation.Trim();
        material.PreferredSupplierId =
            NormalizeOptional(dto.PreferredSupplierId);
        material.PreferredSupplierName =
            NormalizeOptional(dto.PreferredSupplierName);
        material.IsActive =
            dto.IsActive;
        material.UpdatedAt =
            DateTime.UtcNow;
        material.UpdatedBy =
            GetCurrentUserId();

        await _db.RawMaterials.ReplaceOneAsync(
            item =>
                item.Id == id &&
                !item.IsDeleted,
            material
        );

        return Ok(
            ApiResponse<RawMaterial>.Ok(
                material,
                "Materia prima actualizada correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/RawMaterials/{id}/adjust-stock
    // =========================================================
    [HttpPost("{id}/adjust-stock")]
    public async Task<IActionResult> AdjustStock(
        string id,
        [FromBody] RawMaterialStockAdjustmentDto dto)
    {
        if (!AllowedMovementTypes.Contains(
            dto.MovementType))
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    "El tipo de movimiento no es válido"
                )
            );
        }

        if (dto.Quantity <= 0)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    "La cantidad debe ser mayor a cero"
                )
            );
        }

        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    "Debes indicar el motivo del movimiento"
                )
            );
        }

        var material = await _db.RawMaterials
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (material == null)
        {
            return NotFound(
                ApiResponse<RawMaterial>.Fail(
                    "Materia prima no encontrada"
                )
            );
        }

        if (!material.IsActive)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    "No se puede modificar el stock de una materia prima inactiva"
                )
            );
        }

        var previousStock = material.CurrentStock;

        decimal newStock;

        if (dto.MovementType == "Exit")
        {
            if (material.CurrentStock < dto.Quantity)
            {
                return BadRequest(
                    ApiResponse<RawMaterial>.Fail(
                        $"Stock insuficiente. Disponible: " +
                        $"{material.CurrentStock} {material.Unit}"
                    )
                );
            }

            newStock =
                material.CurrentStock - dto.Quantity;
        }
        else
        {
            newStock =
                material.CurrentStock + dto.Quantity;
        }

        if (material.MaximumStock > 0 &&
            newStock > material.MaximumStock)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    $"El movimiento superaría el stock máximo " +
                    $"de {material.MaximumStock} {material.Unit}"
                )
            );
        }

        var unitCost =
            dto.UnitCost ?? material.AverageCost;

        /*
         * Para entradas con costo, recalculamos el
         * costo promedio ponderado.
         */
        if (dto.MovementType == "Entry" &&
            dto.UnitCost.HasValue)
        {
            var previousValue =
                material.CurrentStock *
                material.AverageCost;

            var incomingValue =
                dto.Quantity *
                dto.UnitCost.Value;

            material.AverageCost =
                newStock == 0
                    ? dto.UnitCost.Value
                    : (previousValue + incomingValue) /
                      newStock;

            material.LastPurchaseCost =
                dto.UnitCost.Value;
        }

        material.CurrentStock = newStock;
        material.UpdatedAt = DateTime.UtcNow;
        material.UpdatedBy = GetCurrentUserId();

        await _db.RawMaterials.ReplaceOneAsync(
            item =>
                item.Id == id &&
                !item.IsDeleted,
            material
        );

        await RegisterMovementAsync(
            material,
            dto.MovementType,
            dto.Quantity,
            previousStock,
            newStock,
            dto.Reason.Trim(),
            unitCost,
            dto.ReferenceType.Trim(),
            NormalizeOptional(dto.ReferenceId)
        );

        return Ok(
            ApiResponse<RawMaterial>.Ok(
                material,
                "Movimiento de inventario registrado correctamente"
            )
        );
    }

    // =========================================================
    // Compatibilidad con los endpoints anteriores.
    // PUT: api/RawMaterials/{id}/add-stock
    // =========================================================
    [HttpPut("{id}/add-stock")]
    public async Task<IActionResult> AddStock(
        string id,
        [FromBody] RawMaterialStockUpdateDto dto)
    {
        return await ExecuteLegacyMovement(
            id,
            dto.Quantity,
            "Entry",
            "Entrada manual de inventario"
        );
    }

    // =========================================================
    // PUT: api/RawMaterials/{id}/remove-stock
    // =========================================================
    [HttpPut("{id}/remove-stock")]
    public async Task<IActionResult> RemoveStock(
        string id,
        [FromBody] RawMaterialStockUpdateDto dto)
    {
        return await ExecuteLegacyMovement(
            id,
            dto.Quantity,
            "Exit",
            "Salida manual de inventario"
        );
    }

    // =========================================================
    // DELETE: api/RawMaterials/{id}
    // =========================================================
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var material = await _db.RawMaterials
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (material == null)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Materia prima no encontrada"
                )
            );
        }

        if (material.CurrentStock > 0)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "No se puede eliminar una materia prima que todavía tiene existencias"
                )
            );
        }

        var update = Builders<RawMaterial>.Update
            .Set(item => item.IsDeleted, true)
            .Set(item => item.IsActive, false)
            .Set(item => item.UpdatedAt, DateTime.UtcNow)
            .Set(item => item.UpdatedBy, GetCurrentUserId());

        await _db.RawMaterials.UpdateOneAsync(
            item =>
                item.Id == id &&
                !item.IsDeleted,
            update
        );

        return Ok(
            ApiResponse<string>.Ok(
                "Materia prima eliminada correctamente"
            )
        );
    }

    private async Task<IActionResult> ExecuteLegacyMovement(
        string id,
        decimal quantity,
        string movementType,
        string reason)
    {
        if (quantity <= 0)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    "La cantidad debe ser mayor a cero"
                )
            );
        }

        var material = await _db.RawMaterials
            .Find(item =>
                item.Id == id &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (material == null)
        {
            return NotFound(
                ApiResponse<RawMaterial>.Fail(
                    "Materia prima no encontrada"
                )
            );
        }

        var previousStock = material.CurrentStock;

        var newStock = movementType == "Exit"
            ? previousStock - quantity
            : previousStock + quantity;

        if (newStock < 0)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    "Stock insuficiente"
                )
            );
        }

        if (material.MaximumStock > 0 &&
            newStock > material.MaximumStock)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    "La entrada supera el stock máximo configurado"
                )
            );
        }

        material.CurrentStock = newStock;
        material.UpdatedAt = DateTime.UtcNow;
        material.UpdatedBy = GetCurrentUserId();

        await _db.RawMaterials.ReplaceOneAsync(
            item => item.Id == id,
            material
        );

        await RegisterMovementAsync(
            material,
            movementType,
            quantity,
            previousStock,
            newStock,
            reason,
            material.AverageCost,
            "Manual",
            null
        );

        return Ok(
            ApiResponse<RawMaterial>.Ok(
                material,
                "Stock actualizado correctamente"
            )
        );
    }

    private async Task RegisterMovementAsync(
        RawMaterial material,
        string movementType,
        decimal quantity,
        decimal previousStock,
        decimal newStock,
        string reason,
        decimal unitCost,
        string referenceType,
        string? referenceId)
    {
        var movement = new RawMaterialMovement
        {
            RawMaterialId = material.Id,
            RawMaterialCode = material.Code,
            RawMaterialName = material.Name,
            MovementType = movementType,
            Quantity = quantity,
            PreviousStock = previousStock,
            NewStock = newStock,
            Unit = material.Unit,
            Reason = reason,
            ReferenceType = string.IsNullOrWhiteSpace(
                referenceType)
                    ? "Manual"
                    : referenceType,
            ReferenceId = referenceId,
            UnitCost = unitCost,
            TotalCost = quantity * unitCost,
            MovementDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = GetCurrentUserId()
        };

        await _db.RawMaterialMovements
            .InsertOneAsync(movement);
    }

    private static string? ValidateMaterial(
        RawMaterialCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            return "El código es obligatorio";

        if (dto.Code.Trim().Length > 30)
            return "El código no puede superar los 30 caracteres";

        if (string.IsNullOrWhiteSpace(dto.Name))
            return "El nombre es obligatorio";

        if (dto.Name.Trim().Length < 3)
            return "El nombre debe tener al menos 3 caracteres";

        if (dto.Name.Trim().Length > 150)
            return "El nombre no puede superar los 150 caracteres";

        if (dto.Description.Trim().Length > 500)
            return "La descripción no puede superar los 500 caracteres";

        if (!AllowedCategories.Contains(dto.Category))
            return "La categoría de materia prima no es válida";

        if (string.IsNullOrWhiteSpace(dto.Unit))
            return "La unidad de medida es obligatoria";

        if (dto.CurrentStock < 0)
            return "El stock actual no puede ser negativo";

        if (dto.MinimumStock < 0)
            return "El stock mínimo no puede ser negativo";

        if (dto.MaximumStock < 0)
            return "El stock máximo no puede ser negativo";

        if (dto.MaximumStock > 0 &&
            dto.MaximumStock < dto.MinimumStock)
        {
            return "El stock máximo no puede ser menor al stock mínimo";
        }

        if (dto.MaximumStock > 0 &&
            dto.CurrentStock > dto.MaximumStock)
        {
            return "El stock actual no puede superar el stock máximo";
        }

        if (dto.AverageCost < 0)
            return "El costo promedio no puede ser negativo";

        if (dto.LastPurchaseCost < 0)
            return "El último costo no puede ser negativo";

        if (dto.StorageLocation.Trim().Length > 100)
            return "La ubicación no puede superar los 100 caracteres";

        return null;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }

    private static string NormalizeCode(string value)
    {
        return value
            .Trim()
            .ToUpperInvariant()
            .Replace(" ", "-");
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}