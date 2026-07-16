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

    /*
     * Adjustment se elimina de este endpoint.
     *
     * Una corrección administrativa debe indicar si
     * incrementa o disminuye inventario.
     */
    private static readonly string[]
        AllowedManualMovementTypes =
        {
            "Entry",
            "Exit"
        };

    private readonly MongoDbService _db;

    public RawMaterialsController(
        MongoDbService db)
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
    public async Task<IActionResult> GetById(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    "El identificador de la materia prima es obligatorio"
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

        var totalMaterials =
            materials.Count;

        var activeMaterials =
            materials.Count(material =>
                material.IsActive
            );

        var lowStockMaterials =
            materials.Count(material =>
                material.IsActive &&
                material.CurrentStock <=
                    material.MinimumStock
            );

        var outOfStockMaterials =
            materials.Count(material =>
                material.IsActive &&
                material.CurrentStock <= 0
            );

        var recycledMaterials =
            materials.Count(material =>
                material.IsRecycled
            );

        var reusableMaterials =
            materials.Count(material =>
                material.IsReusable
            );

        var totalInventoryValue =
            InventoryRoundingService.RoundMoney(
                materials.Sum(material =>
                    material.CurrentStock *
                    material.AverageCost
                )
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
                material.CurrentStock <=
                    material.MinimumStock)
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
    public async Task<IActionResult> GetMovements(
        string id)
    {
        var materialExists =
            await _db.RawMaterials
                .Find(material =>
                    material.Id == id &&
                    !material.IsDeleted)
                .AnyAsync();

        if (!materialExists)
        {
            return NotFound(
                ApiResponse<
                    List<RawMaterialMovement>
                >.Fail(
                    "Materia prima no encontrada"
                )
            );
        }

        var movements =
            await _db.RawMaterialMovements
                .Find(movement =>
                    movement.RawMaterialId == id &&
                    !movement.IsDeleted)
                .SortByDescending(movement =>
                    movement.MovementDate)
                .Limit(200)
                .ToListAsync();

        return Ok(
            ApiResponse<
                List<RawMaterialMovement>
            >.Ok(
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
        var structuralError =
            ValidateMaterialStructure(dto);

        if (structuralError != null)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    structuralError
                )
            );
        }

        var unitResult =
            await GetActiveUnitAsync(
                dto.UnitOfMeasureId
            );

        if (unitResult.Unit == null)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    unitResult.ErrorMessage!
                )
            );
        }

        var unit = unitResult.Unit;

        var quantityError =
            ValidateMaterialQuantities(
                dto,
                unit
            );

        if (quantityError != null)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    quantityError
                )
            );
        }

        var costError =
            ValidateMaterialCosts(dto);

        if (costError != null)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    costError
                )
            );
        }

        var supplierResult =
            await ResolvePreferredSupplierAsync(
                dto.PreferredSupplierId
            );

        if (!supplierResult.Success)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    supplierResult.ErrorMessage!
                )
            );
        }

        var normalizedCode =
            NormalizeCode(dto.Code);

        var normalizedName =
            dto.Name.Trim();

        var codeExists =
            await _db.RawMaterials
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

        var nameExists =
            await _db.RawMaterials
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

        var now = DateTime.UtcNow;

        var material = new RawMaterial
        {
            Code = normalizedCode,
            Name = normalizedName,

            Description =
                dto.Description.Trim(),

            Category =
                dto.Category.Trim(),

            UnitOfMeasureId =
                unit.Id,

            UnitCode =
                unit.Code,

            UnitName =
                unit.SingularName,

            UnitSymbol =
                unit.Symbol,

            UnitAllowsDecimals =
                unit.AllowsDecimals,

            UnitDecimalPlaces =
                unit.DecimalPlaces,

            Unit =
                unit.Symbol,

            CurrentStock =
                NormalizeQuantity(
                    dto.CurrentStock,
                    unit
                ),

            MinimumStock =
                NormalizeQuantity(
                    dto.MinimumStock,
                    unit
                ),

            MaximumStock =
                NormalizeQuantity(
                    dto.MaximumStock,
                    unit
                ),

            AverageCost =
                InventoryRoundingService
                    .RoundUnitCost(
                        dto.AverageCost
                    ),

            LastPurchaseCost =
                InventoryRoundingService
                    .RoundUnitCost(
                        dto.LastPurchaseCost
                    ),

            IsRecycled =
                dto.IsRecycled,

            IsReusable =
                dto.IsReusable,

            RequiresPurchase =
                dto.RequiresPurchase,

            StorageLocation =
                dto.StorageLocation.Trim(),

            PreferredSupplierId =
                supplierResult.Supplier?.Id,

            PreferredSupplierName =
                supplierResult.Supplier?.Name,

            IsActive = true,
            IsDeleted = false,
            CreatedAt = now,
            CreatedBy = GetCurrentUserId()
        };

        using var session =
            await _db.StartSessionAsync();

        try
        {
            session.StartTransaction();

            await _db.RawMaterials.InsertOneAsync(
                session,
                material
            );

            if (material.CurrentStock > 0)
            {
                var movement =
                    BuildMovement(
                        material,
                        movementType: "Entry",
                        quantity:
                            material.CurrentStock,
                        previousStock: 0,
                        newStock:
                            material.CurrentStock,
                        reason:
                            "Inventario inicial",
                        unitCost:
                            material.AverageCost,
                        referenceType:
                            "InitialStock",
                        referenceId: null
                    );

                await _db.RawMaterialMovements
                    .InsertOneAsync(
                        session,
                        movement
                    );
            }

            await session.CommitTransactionAsync();
        }
        catch
        {
            await session.AbortTransactionAsync();
            throw;
        }

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                id = material.Id
            },
            ApiResponse<RawMaterial>.Ok(
                material,
                "Materia prima creada correctamente"
            )
        );
    }

    // =========================================================
    // PUT: api/RawMaterials/{id}
    //
    // El stock actual nunca se reemplaza desde aquí.
    // =========================================================
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] RawMaterialUpdateDto dto)
    {
        var structuralError =
            ValidateMaterialStructure(dto);

        if (structuralError != null)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    structuralError
                )
            );
        }

        var material =
            await _db.RawMaterials
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

        var unitResult =
            await GetActiveUnitAsync(
                dto.UnitOfMeasureId
            );

        if (unitResult.Unit == null)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    unitResult.ErrorMessage!
                )
            );
        }

        var unit = unitResult.Unit;

        var hasMovements =
            await _db.RawMaterialMovements
                .Find(movement =>
                    movement.RawMaterialId ==
                        material.Id &&
                    !movement.IsDeleted)
                .AnyAsync();

        if (hasMovements &&
            material.UnitOfMeasureId != unit.Id)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    "No se puede cambiar la unidad de una materia prima que ya tiene movimientos de inventario"
                )
            );
        }

        /*
         * En edición se valida el stock actual real,
         * no el valor CurrentStock recibido por el DTO.
         */
        var quantityError =
            ValidateUpdateQuantities(
                material.CurrentStock,
                dto.MinimumStock,
                dto.MaximumStock,
                unit
            );

        if (quantityError != null)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    quantityError
                )
            );
        }

        var costError =
            ValidateMaterialCosts(dto);

        if (costError != null)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    costError
                )
            );
        }

        var supplierResult =
            await ResolvePreferredSupplierAsync(
                dto.PreferredSupplierId
            );

        if (!supplierResult.Success)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    supplierResult.ErrorMessage!
                )
            );
        }

        var normalizedCode =
            NormalizeCode(dto.Code);

        var normalizedName =
            dto.Name.Trim();

        var duplicateCode =
            await _db.RawMaterials
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

        var duplicateName =
            await _db.RawMaterials
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

        material.Code =
            normalizedCode;

        material.Name =
            normalizedName;

        material.Description =
            dto.Description.Trim();

        material.Category =
            dto.Category.Trim();

        material.UnitOfMeasureId =
            unit.Id;

        material.UnitCode =
            unit.Code;

        material.UnitName =
            unit.SingularName;

        material.UnitSymbol =
            unit.Symbol;

        material.UnitAllowsDecimals =
            unit.AllowsDecimals;

        material.UnitDecimalPlaces =
            unit.DecimalPlaces;

        material.Unit =
            unit.Symbol;

        material.MinimumStock =
            NormalizeQuantity(
                dto.MinimumStock,
                unit
            );

        material.MaximumStock =
            NormalizeQuantity(
                dto.MaximumStock,
                unit
            );

        /*
         * AverageCost y LastPurchaseCost se conservan
         * por compatibilidad administrativa durante
         * este bloque.
         *
         * Cuando se corrija Compras, su modificación
         * normal ocurrirá únicamente desde compras y
         * movimientos autorizados.
         */
        material.AverageCost =
            InventoryRoundingService
                .RoundUnitCost(
                    dto.AverageCost
                );

        material.LastPurchaseCost =
            InventoryRoundingService
                .RoundUnitCost(
                    dto.LastPurchaseCost
                );

        material.IsRecycled =
            dto.IsRecycled;

        material.IsReusable =
            dto.IsReusable;

        material.RequiresPurchase =
            dto.RequiresPurchase;

        material.StorageLocation =
            dto.StorageLocation.Trim();

        material.PreferredSupplierId =
            supplierResult.Supplier?.Id;

        material.PreferredSupplierName =
            supplierResult.Supplier?.Name;

        material.IsActive =
            dto.IsActive;

        material.UpdatedAt =
            DateTime.UtcNow;

        material.UpdatedBy =
            GetCurrentUserId();

        var result =
            await _db.RawMaterials
                .ReplaceOneAsync(
                    item =>
                        item.Id == id &&
                        !item.IsDeleted,
                    material
                );

        if (result.MatchedCount == 0)
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
                "Materia prima actualizada correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/RawMaterials/{id}/adjust-stock
    //
    // Aunque la ruta conserva el nombre adjust-stock,
    // MovementType solamente permite Entry o Exit.
    // =========================================================
    [HttpPost("{id}/adjust-stock")]
    public async Task<IActionResult> AdjustStock(
        string id,
        [FromBody]
        RawMaterialStockAdjustmentDto dto)
    {
        var movementType =
     dto.MovementType?.Trim();

        if (string.IsNullOrWhiteSpace(
                movementType))
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    "El tipo de movimiento es obligatorio"
                )
            );
        }

        if (!AllowedManualMovementTypes.Contains(
                movementType))
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    "El tipo de movimiento debe ser Entry o Exit"
                )
            );
        }

        var material =
            await _db.RawMaterials
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

        var unit =
            await ResolveMaterialUnitAsync(
                material
            );

        if (unit == null)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    "La materia prima no tiene una unidad de medida válida"
                )
            );
        }

        var quantityError =
            QuantityValidationService
                .ValidateQuantity(
                    dto.Quantity,
                    unit,
                    "La cantidad"
                );

        if (quantityError != null)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    quantityError
                )
            );
        }

        var normalizedQuantity =
            NormalizeQuantity(
                dto.Quantity,
                unit
            );

        decimal unitCost;

        if (dto.UnitCost.HasValue)
        {
            var costError =
                QuantityValidationService
                    .ValidateCost(
                        dto.UnitCost.Value,
                        "El costo unitario"
                    );

            if (costError != null)
            {
                return BadRequest(
                    ApiResponse<RawMaterial>.Fail(
                        costError
                    )
                );
            }

            unitCost =
                InventoryRoundingService
                    .RoundUnitCost(
                        dto.UnitCost.Value
                    );
        }
        else
        {
            unitCost =
                material.AverageCost;
        }

        var previousStock =
            material.CurrentStock;

        var newStock =
            movementType == "Exit"
                ? previousStock -
                    normalizedQuantity
                : previousStock +
                    normalizedQuantity;

        newStock =
            NormalizeQuantity(
                newStock,
                unit
            );

        if (newStock < 0)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    $"Stock insuficiente. Disponible: " +
                    $"{FormatQuantity(previousStock, unit)} " +
                    $"{unit.Symbol}"
                )
            );
        }

        if (material.MaximumStock > 0 &&
            newStock > material.MaximumStock)
        {
            return BadRequest(
                ApiResponse<RawMaterial>.Fail(
                    $"El movimiento superaría el stock máximo de " +
                    $"{FormatQuantity(material.MaximumStock, unit)} " +
                    $"{unit.Symbol}"
                )
            );
        }

        /*
         * Una entrada manual con costo puede modificar
         * el costo promedio ponderado.
         *
         * Una salida no modifica el costo promedio.
         */
        var newAverageCost =
            material.AverageCost;

        var newLastPurchaseCost =
            material.LastPurchaseCost;

        if (movementType == "Entry" &&
            dto.UnitCost.HasValue)
        {
            var previousValue =
                previousStock *
                material.AverageCost;

            var incomingValue =
                normalizedQuantity *
                unitCost;

            newAverageCost =
                newStock == 0
                    ? unitCost
                    : (
                        previousValue +
                        incomingValue
                      ) / newStock;

            newAverageCost =
                InventoryRoundingService
                    .RoundUnitCost(
                        newAverageCost
                    );

            newLastPurchaseCost =
                unitCost;
        }

        using var session =
            await _db.StartSessionAsync();

        try
        {
            session.StartTransaction();

            /*
             * Se incluye PreviousStock en el filtro.
             *
             * Si otra operación cambió el inventario
             * después de la lectura, este update no
             * modificará el documento.
             */
            var update =
                Builders<RawMaterial>.Update
                    .Set(
                        item =>
                            item.CurrentStock,
                        newStock
                    )
                    .Set(
                        item =>
                            item.AverageCost,
                        newAverageCost
                    )
                    .Set(
                        item =>
                            item.LastPurchaseCost,
                        newLastPurchaseCost
                    )
                    .Set(
                        item =>
                            item.UpdatedAt,
                        DateTime.UtcNow
                    )
                    .Set(
                        item =>
                            item.UpdatedBy,
                        GetCurrentUserId()
                    );

            var updateResult =
                await _db.RawMaterials
                    .UpdateOneAsync(
                        session,
                        item =>
                            item.Id == id &&
                            !item.IsDeleted &&
                            item.IsActive &&
                            item.CurrentStock ==
                                previousStock,
                        update
                    );

            if (updateResult.ModifiedCount == 0)
            {
                await session
                    .AbortTransactionAsync();

                return Conflict(
                    ApiResponse<RawMaterial>.Fail(
                        "El inventario cambió mientras se procesaba la operación. Vuelve a intentarlo."
                    )
                );
            }

            material.CurrentStock =
                newStock;

            material.AverageCost =
                newAverageCost;

            material.LastPurchaseCost =
                newLastPurchaseCost;

            material.UpdatedAt =
                DateTime.UtcNow;

            material.UpdatedBy =
                GetCurrentUserId();

            var movement =
                BuildMovement(
                    material,
                    movementType,
                    normalizedQuantity,
                    previousStock,
                    newStock,
                    dto.Reason.Trim(),
                    unitCost,
                    NormalizeReferenceType(
                        dto.ReferenceType
                    ),
                    NormalizeOptional(
                        dto.ReferenceId
                    )
                );

            await _db.RawMaterialMovements
                .InsertOneAsync(
                    session,
                    movement
                );

            await session.CommitTransactionAsync();
        }
        catch
        {
            await session.AbortTransactionAsync();
            throw;
        }

        return Ok(
            ApiResponse<RawMaterial>.Ok(
                material,
                "Movimiento de inventario registrado correctamente"
            )
        );
    }

    // =========================================================
    // PUT: api/RawMaterials/{id}/add-stock
    //
    // Compatibilidad temporal.
    // =========================================================
    [HttpPut("{id}/add-stock")]
    public async Task<IActionResult> AddStock(
        string id,
        [FromBody]
        RawMaterialStockUpdateDto dto)
    {
        return await ExecuteLegacyMovementAsync(
            id,
            dto.Quantity,
            "Entry",
            "Entrada manual de inventario"
        );
    }

    // =========================================================
    // PUT: api/RawMaterials/{id}/remove-stock
    //
    // Compatibilidad temporal.
    // =========================================================
    [HttpPut("{id}/remove-stock")]
    public async Task<IActionResult> RemoveStock(
        string id,
        [FromBody]
        RawMaterialStockUpdateDto dto)
    {
        return await ExecuteLegacyMovementAsync(
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
    public async Task<IActionResult> Delete(
        string id)
    {
        var material =
            await _db.RawMaterials
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

        var usedInRecipes =
            await _db.Recipes
                .Find(recipe =>
                    !recipe.IsDeleted &&
                    recipe.Details.Any(detail =>
                        detail.RawMaterialId == id
                    ))
                .AnyAsync();

        if (usedInRecipes)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "No se puede eliminar una materia prima utilizada en recetas. Puedes desactivarla."
                )
            );
        }

        var usedInPurchases =
            await _db.Purchases
                .Find(purchase =>
                    !purchase.IsDeleted &&
                    purchase.Details.Any(detail =>
                        detail.RawMaterialId == id
                    ))
                .AnyAsync();

        if (usedInPurchases)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "No se puede eliminar una materia prima con compras registradas. Puedes desactivarla."
                )
            );
        }

        var usedInProduction =
            await _db.ProductionOrders
                .Find(order =>
                    !order.IsDeleted &&
                    order.Materials.Any(item =>
                        item.RawMaterialId == id
                    ))
                .AnyAsync();

        if (usedInProduction)
        {
            return BadRequest(
                ApiResponse<string>.Fail(
                    "No se puede eliminar una materia prima utilizada en producción. Puedes desactivarla."
                )
            );
        }

        var update =
            Builders<RawMaterial>.Update
                .Set(
                    item =>
                        item.IsDeleted,
                    true
                )
                .Set(
                    item =>
                        item.IsActive,
                    false
                )
                .Set(
                    item =>
                        item.UpdatedAt,
                    DateTime.UtcNow
                )
                .Set(
                    item =>
                        item.UpdatedBy,
                    GetCurrentUserId()
                );

        var result =
            await _db.RawMaterials
                .UpdateOneAsync(
                    item =>
                        item.Id == id &&
                        !item.IsDeleted,
                    update
                );

        if (result.MatchedCount == 0)
        {
            return NotFound(
                ApiResponse<string>.Fail(
                    "Materia prima no encontrada"
                )
            );
        }

        return Ok(
            ApiResponse<string>.Ok(
                "Materia prima eliminada correctamente"
            )
        );
    }

    private async Task<IActionResult>
        ExecuteLegacyMovementAsync(
            string id,
            decimal quantity,
            string movementType,
            string reason)
    {
        var dto =
            new RawMaterialStockAdjustmentDto
            {
                MovementType =
                    movementType,

                Quantity =
                    quantity,

                Reason =
                    reason,

                ReferenceType =
                    "Manual"
            };

        return await AdjustStock(
            id,
            dto
        );
    }

    private async Task<UnitLookupResult>
        GetActiveUnitAsync(
            string unitOfMeasureId)
    {
        if (string.IsNullOrWhiteSpace(
                unitOfMeasureId))
        {
            return UnitLookupResult.Fail(
                "La unidad de medida es obligatoria"
            );
        }

        var unit =
            await _db.UnitsOfMeasure
                .Find(item =>
                    item.Id == unitOfMeasureId &&
                    !item.IsDeleted &&
                    item.IsActive)
                .FirstOrDefaultAsync();

        return unit == null
            ? UnitLookupResult.Fail(
                "La unidad de medida no existe o está inactiva"
            )
            : UnitLookupResult.Ok(unit);
    }

    private async Task<UnitOfMeasure?>
        ResolveMaterialUnitAsync(
            RawMaterial material)
    {
        if (!string.IsNullOrWhiteSpace(
                material.UnitOfMeasureId))
        {
            return await _db.UnitsOfMeasure
                .Find(unit =>
                    unit.Id ==
                        material.UnitOfMeasureId &&
                    !unit.IsDeleted)
                .FirstOrDefaultAsync();
        }

        /*
         * Compatibilidad temporal durante la migración.
         */
        return await _db.UnitsOfMeasure
            .Find(unit =>
                !unit.IsDeleted &&
                (
                    unit.Code == material.Unit ||
                    unit.Symbol == material.Unit ||
                    unit.SingularName ==
                        material.Unit ||
                    unit.PluralName ==
                        material.Unit
                ))
            .FirstOrDefaultAsync();
    }

    private async Task<SupplierLookupResult>
        ResolvePreferredSupplierAsync(
            string? supplierId)
    {
        if (string.IsNullOrWhiteSpace(
                supplierId))
        {
            return SupplierLookupResult.Ok(null);
        }

        var supplier =
            await _db.Suppliers
                .Find(item =>
                    item.Id == supplierId &&
                    !item.IsDeleted &&
                    item.IsActive)
                .FirstOrDefaultAsync();

        if (supplier == null)
        {
            return SupplierLookupResult.Fail(
                "El proveedor preferido no existe o está inactivo"
            );
        }

        return SupplierLookupResult.Ok(
            supplier
        );
    }

    private RawMaterialMovement BuildMovement(
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
        return new RawMaterialMovement
        {
            RawMaterialId =
                material.Id,

            RawMaterialCode =
                material.Code,

            RawMaterialName =
                material.Name,

            MovementType =
                movementType,

            Quantity =
                quantity,

            PreviousStock =
                previousStock,

            NewStock =
                newStock,

            UnitOfMeasureId =
                material.UnitOfMeasureId,

            UnitCode =
                material.UnitCode,

            UnitName =
                material.UnitName,

            UnitSymbol =
                material.UnitSymbol,

            UnitAllowsDecimals =
                material.UnitAllowsDecimals,

            UnitDecimalPlaces =
                material.UnitDecimalPlaces,

            Unit =
                material.UnitSymbol,

            Reason =
                reason,

            ReferenceType =
                referenceType,

            ReferenceId =
                referenceId,

            UnitCost =
                InventoryRoundingService
                    .RoundUnitCost(
                        unitCost
                    ),

            TotalCost =
                InventoryRoundingService
                    .RoundEstimatedCost(
                        quantity * unitCost
                    ),

            MovementDate =
                DateTime.UtcNow,

            CreatedAt =
                DateTime.UtcNow,

            CreatedBy =
                GetCurrentUserId(),

            IsDeleted =
                false
        };
    }

    private static string?
        ValidateMaterialStructure(
            RawMaterialCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return "El código es obligatorio";
        }

        if (dto.Code.Trim().Length > 30)
        {
            return
                "El código no puede superar los 30 caracteres";
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return "El nombre es obligatorio";
        }

        if (dto.Name.Trim().Length < 3)
        {
            return
                "El nombre debe tener al menos 3 caracteres";
        }

        if (dto.Name.Trim().Length > 150)
        {
            return
                "El nombre no puede superar los 150 caracteres";
        }

        if (dto.Description?.Trim().Length > 500)
        {
            return
                "La descripción no puede superar los 500 caracteres";
        }

        if (!AllowedCategories.Contains(
                dto.Category))
        {
            return
                "La categoría de materia prima no es válida";
        }

        if (string.IsNullOrWhiteSpace(
                dto.UnitOfMeasureId))
        {
            return
                "La unidad de medida es obligatoria";
        }

        if (dto.StorageLocation?
                .Trim()
                .Length > 100)
        {
            return
                "La ubicación no puede superar los 100 caracteres";
        }

        return null;
    }

    private static string?
        ValidateMaterialQuantities(
            RawMaterialCreateDto dto,
            UnitOfMeasure unit)
    {
        var currentError =
            QuantityValidationService
                .ValidateQuantity(
                    dto.CurrentStock,
                    unit,
                    "El stock actual",
                    positiveRequired: false
                );

        if (currentError != null)
        {
            return currentError;
        }

        var minimumError =
            QuantityValidationService
                .ValidateQuantity(
                    dto.MinimumStock,
                    unit,
                    "El stock mínimo",
                    positiveRequired: false
                );

        if (minimumError != null)
        {
            return minimumError;
        }

        var maximumError =
            QuantityValidationService
                .ValidateQuantity(
                    dto.MaximumStock,
                    unit,
                    "El stock máximo",
                    positiveRequired: false
                );

        if (maximumError != null)
        {
            return maximumError;
        }

        return ValidateStockLimits(
            dto.CurrentStock,
            dto.MinimumStock,
            dto.MaximumStock
        );
    }

    private static string?
        ValidateUpdateQuantities(
            decimal currentStock,
            decimal minimumStock,
            decimal maximumStock,
            UnitOfMeasure unit)
    {
        var currentError =
            QuantityValidationService
                .ValidateQuantity(
                    currentStock,
                    unit,
                    "El stock actual",
                    positiveRequired: false
                );

        if (currentError != null)
        {
            return currentError;
        }

        var minimumError =
            QuantityValidationService
                .ValidateQuantity(
                    minimumStock,
                    unit,
                    "El stock mínimo",
                    positiveRequired: false
                );

        if (minimumError != null)
        {
            return minimumError;
        }

        var maximumError =
            QuantityValidationService
                .ValidateQuantity(
                    maximumStock,
                    unit,
                    "El stock máximo",
                    positiveRequired: false
                );

        if (maximumError != null)
        {
            return maximumError;
        }

        return ValidateStockLimits(
            currentStock,
            minimumStock,
            maximumStock
        );
    }

    private static string?
        ValidateStockLimits(
            decimal currentStock,
            decimal minimumStock,
            decimal maximumStock)
    {
        if (maximumStock > 0 &&
            maximumStock < minimumStock)
        {
            return
                "El stock máximo no puede ser menor al stock mínimo";
        }

        if (maximumStock > 0 &&
            currentStock > maximumStock)
        {
            return
                "El stock actual no puede superar el stock máximo";
        }

        return null;
    }

    private static string?
        ValidateMaterialCosts(
            RawMaterialCreateDto dto)
    {
        var averageCostError =
            QuantityValidationService
                .ValidateCost(
                    dto.AverageCost,
                    "El costo promedio"
                );

        if (averageCostError != null)
        {
            return averageCostError;
        }

        var lastCostError =
            QuantityValidationService
                .ValidateCost(
                    dto.LastPurchaseCost,
                    "El último costo de compra"
                );

        return lastCostError;
    }

    private static decimal NormalizeQuantity(
        decimal quantity,
        UnitOfMeasure unit)
    {
        if (!unit.AllowsDecimals)
        {
            return decimal.Truncate(quantity);
        }

        return decimal.Round(
            quantity,
            unit.DecimalPlaces,
            MidpointRounding.AwayFromZero
        );
    }

    private static string FormatQuantity(
        decimal quantity,
        UnitOfMeasure unit)
    {
        if (!unit.AllowsDecimals)
        {
            return decimal
                .Truncate(quantity)
                .ToString("0");
        }

        var format =
            unit.DecimalPlaces == 0
                ? "0"
                : "0." +
                  new string(
                      '#',
                      unit.DecimalPlaces
                  );

        return quantity.ToString(format);
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }

    private static string NormalizeCode(
        string value)
    {
        return value
            .Trim()
            .ToUpperInvariant()
            .Replace(" ", "-");
    }

    private static string NormalizeReferenceType(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Manual"
            : value.Trim();
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private sealed class UnitLookupResult
    {
        public UnitOfMeasure? Unit { get; init; }

        public string? ErrorMessage { get; init; }

        public static UnitLookupResult Ok(
            UnitOfMeasure unit)
        {
            return new UnitLookupResult
            {
                Unit = unit
            };
        }

        public static UnitLookupResult Fail(
            string message)
        {
            return new UnitLookupResult
            {
                ErrorMessage = message
            };
        }
    }

    private sealed class SupplierLookupResult
    {
        public bool Success { get; init; }

        public Supplier? Supplier { get; init; }

        public string? ErrorMessage { get; init; }

        public static SupplierLookupResult Ok(
            Supplier? supplier)
        {
            return new SupplierLookupResult
            {
                Success = true,
                Supplier = supplier
            };
        }

        public static SupplierLookupResult Fail(
            string message)
        {
            return new SupplierLookupResult
            {
                Success = false,
                ErrorMessage = message
            };
        }
    }
}