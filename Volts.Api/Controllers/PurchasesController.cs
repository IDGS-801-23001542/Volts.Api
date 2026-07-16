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
public class PurchasesController : ControllerBase
{
    private readonly MongoDbService _db;

    public PurchasesController(
        MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // GET: api/Purchases
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var purchases =
            await _db.Purchases
                .Find(item =>
                    !item.IsDeleted)
                .SortByDescending(item =>
                    item.PurchaseDate)
                .ToListAsync();

        return Ok(
            ApiResponse<List<Purchase>>.Ok(
                purchases,
                "Compras obtenidas correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/Purchases/summary
    // =========================================================
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var purchases =
            await _db.Purchases
                .Find(item =>
                    !item.IsDeleted &&
                    item.Status == "Completed")
                .ToListAsync();

        var now =
            DateTime.UtcNow;

        var currentMonthPurchases =
            purchases
                .Where(item =>
                    item.PurchaseDate.Year ==
                        now.Year &&
                    item.PurchaseDate.Month ==
                        now.Month)
                .ToList();

        var summary =
            new PurchaseSummaryDto
            {
                TotalPurchases =
                    purchases.Count,

                PurchasesThisMonth =
                    currentMonthPurchases.Count,

                TotalInvested =
                    InventoryRoundingService
                        .RoundMoney(
                            purchases.Sum(
                                item =>
                                    item.Total
                            )
                        ),

                InvestedThisMonth =
                    InventoryRoundingService
                        .RoundMoney(
                            currentMonthPurchases
                                .Sum(
                                    item =>
                                        item.Total
                                )
                        ),

                AveragePurchaseValue =
                    purchases.Count == 0
                        ? 0
                        : InventoryRoundingService
                            .RoundMoney(
                                purchases.Average(
                                    item =>
                                        item.Total
                                )
                            ),

                SuppliersUsed =
                    purchases
                        .Select(item =>
                            item.SupplierId)
                        .Distinct()
                        .Count()
            };

        return Ok(
            ApiResponse<PurchaseSummaryDto>.Ok(
                summary,
                "Resumen de compras obtenido correctamente"
            )
        );
    }

    // =========================================================
    // GET: api/Purchases/{id}
    // =========================================================
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<Purchase>.Fail(
                    "El identificador de la compra es obligatorio"
                )
            );
        }

        var purchase =
            await _db.Purchases
                .Find(item =>
                    item.Id == id &&
                    !item.IsDeleted)
                .FirstOrDefaultAsync();

        if (purchase == null)
        {
            return NotFound(
                ApiResponse<Purchase>.Fail(
                    "Compra no encontrada"
                )
            );
        }

        return Ok(
            ApiResponse<Purchase>.Ok(
                purchase,
                "Compra obtenida correctamente"
            )
        );
    }

    // =========================================================
    // POST: api/Purchases
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] PurchaseCreateDto dto)
    {
        var validationError =
            ValidatePurchase(dto);

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<Purchase>.Fail(
                    validationError
                )
            );
        }

        var supplier =
            await _db.Suppliers
                .Find(item =>
                    item.Id == dto.SupplierId &&
                    !item.IsDeleted &&
                    item.IsActive)
                .FirstOrDefaultAsync();

        if (supplier == null)
        {
            return BadRequest(
                ApiResponse<Purchase>.Fail(
                    "El proveedor no existe o está inactivo"
                )
            );
        }

        var repeatedMaterial =
            dto.Details
                .GroupBy(item =>
                    item.RawMaterialId)
                .FirstOrDefault(group =>
                    group.Count() > 1);

        if (repeatedMaterial != null)
        {
            return BadRequest(
                ApiResponse<Purchase>.Fail(
                    "Una materia prima no puede aparecer más de una vez en la misma compra"
                )
            );
        }

        var materialIds =
            dto.Details
                .Select(item =>
                    item.RawMaterialId)
                .Distinct()
                .ToList();

        var materials =
            await _db.RawMaterials
                .Find(item =>
                    materialIds.Contains(item.Id) &&
                    !item.IsDeleted)
                .ToListAsync();

        if (materials.Count != materialIds.Count)
        {
            return BadRequest(
                ApiResponse<Purchase>.Fail(
                    "Una o más materias primas no existen"
                )
            );
        }

        var units =
            await _db.UnitsOfMeasure
                .Find(item =>
                    !item.IsDeleted)
                .ToListAsync();

        var validatedItems =
            new List<ValidatedPurchaseItem>();

        foreach (var dtoDetail in dto.Details)
        {
            var material =
                materials.First(item =>
                    item.Id ==
                        dtoDetail.RawMaterialId);

            if (!material.IsActive)
            {
                return BadRequest(
                    ApiResponse<Purchase>.Fail(
                        $"La materia prima {material.Name} está inactiva"
                    )
                );
            }

            var unit =
                units.FirstOrDefault(item =>
                    item.Id ==
                        material.UnitOfMeasureId);

            if (unit == null)
            {
                return BadRequest(
                    ApiResponse<Purchase>.Fail(
                        $"La materia prima {material.Name} no tiene una unidad válida"
                    )
                );
            }

            var quantityError =
                QuantityValidationService
                    .ValidateQuantity(
                        dtoDetail.Quantity,
                        unit,
                        $"La cantidad de {material.Name}"
                    );

            if (quantityError != null)
            {
                return BadRequest(
                    ApiResponse<Purchase>.Fail(
                        quantityError
                    )
                );
            }

            var costError =
                QuantityValidationService
                    .ValidateCost(
                        dtoDetail.UnitCost,
                        $"El costo unitario de {material.Name}",
                        positiveRequired: true
                    );

            if (costError != null)
            {
                return BadRequest(
                    ApiResponse<Purchase>.Fail(
                        costError
                    )
                );
            }

            var quantity =
                NormalizeQuantity(
                    dtoDetail.Quantity,
                    unit
                );

            var unitCost =
                InventoryRoundingService
                    .RoundUnitCost(
                        dtoDetail.UnitCost
                    );

            var newStock =
                NormalizeQuantity(
                    material.CurrentStock +
                        quantity,
                    unit
                );

            if (material.MaximumStock > 0 &&
                newStock >
                    material.MaximumStock)
            {
                return BadRequest(
                    ApiResponse<Purchase>.Fail(
                        $"La compra de {material.Name} superaría " +
                        $"el stock máximo de " +
                        $"{FormatQuantity(material.MaximumStock, unit)} " +
                        $"{unit.Symbol}. Stock actual: " +
                        $"{FormatQuantity(material.CurrentStock, unit)} " +
                        $"{unit.Symbol}"
                    )
                );
            }

            var detailSubtotal =
                InventoryRoundingService
                    .RoundMoney(
                        quantity *
                        unitCost
                    );

            var previousInventoryValue =
                material.CurrentStock *
                material.AverageCost;

            var incomingValue =
                quantity *
                unitCost;

            var newAverageCost =
                newStock == 0
                    ? unitCost
                    : (
                        previousInventoryValue +
                        incomingValue
                      ) / newStock;

            newAverageCost =
                InventoryRoundingService
                    .RoundUnitCost(
                        newAverageCost
                    );

            validatedItems.Add(
                new ValidatedPurchaseItem
                {
                    Material =
                        material,

                    Unit =
                        unit,

                    Quantity =
                        quantity,

                    UnitCost =
                        unitCost,

                    Subtotal =
                        detailSubtotal,

                    PreviousStock =
                        material.CurrentStock,

                    NewStock =
                        newStock,

                    PreviousAverageCost =
                        material.AverageCost,

                    NewAverageCost =
                        newAverageCost
                }
            );
        }

        var purchaseDate =
            dto.PurchaseDate?.ToUniversalTime() ??
            DateTime.UtcNow;

        var subtotal =
            InventoryRoundingService
                .RoundMoney(
                    validatedItems.Sum(
                        item =>
                            item.Subtotal
                    )
                );

        var tax =
            InventoryRoundingService
                .RoundMoney(dto.Tax);

        var shippingCost =
            InventoryRoundingService
                .RoundMoney(
                    dto.ShippingCost
                );

        var total =
            InventoryRoundingService
                .RoundMoney(
                    subtotal +
                    tax +
                    shippingCost
                );

        var purchase =
            new Purchase
            {
                Folio =
                    GenerateFolio(
                        purchaseDate
                    ),

                InvoiceNumber =
                    NormalizeOptional(
                        dto.InvoiceNumber
                    ),

                SupplierId =
                    supplier.Id,

                SupplierCode =
                    supplier.Code,

                SupplierName =
                    supplier.Name,

                PurchaseDate =
                    purchaseDate,

                Subtotal =
                    subtotal,

                Tax =
                    tax,

                ShippingCost =
                    shippingCost,

                Total =
                    total,

                Status =
                    "Completed",

                Notes =
                    dto.Notes?.Trim() ??
                    string.Empty,

                Details =
                    validatedItems
                        .Select(item =>
                            BuildDetail(item))
                        .ToList(),

                IsDeleted =
                    false,

                CreatedAt =
                    DateTime.UtcNow,

                CreatedBy =
                    GetCurrentUserId()
            };

        using var session =
            await _db.StartSessionAsync();

        try
        {
            session.StartTransaction();

            await _db.Purchases
                .InsertOneAsync(
                    session,
                    purchase
                );

            foreach (var item in validatedItems)
            {
                var materialUpdate =
                    Builders<RawMaterial>.Update
                        .Set(
                            material =>
                                material.CurrentStock,
                            item.NewStock
                        )
                        .Set(
                            material =>
                                material.AverageCost,
                            item.NewAverageCost
                        )
                        .Set(
                            material =>
                                material.LastPurchaseCost,
                            item.UnitCost
                        )
                        .Set(
                            material =>
                                material.PreferredSupplierId,
                            supplier.Id
                        )
                        .Set(
                            material =>
                                material.PreferredSupplierName,
                            supplier.Name
                        )
                        .Set(
                            material =>
                                material.UpdatedAt,
                            DateTime.UtcNow
                        )
                        .Set(
                            material =>
                                material.UpdatedBy,
                            GetCurrentUserId()
                        );

                var updateResult =
                    await _db.RawMaterials
                        .UpdateOneAsync(
                            session,
                            material =>
                                material.Id ==
                                    item.Material.Id &&
                                !material.IsDeleted &&
                                material.IsActive &&
                                material.CurrentStock ==
                                    item.PreviousStock,
                            materialUpdate
                        );

                if (updateResult.ModifiedCount != 1)
                {
                    throw new InvalidOperationException(
                        $"El inventario de {item.Material.Name} cambió mientras se procesaba la compra"
                    );
                }

                var movement =
                    BuildMovement(
                        item,
                        supplier,
                        purchase
                    );

                await _db.RawMaterialMovements
                    .InsertOneAsync(
                        session,
                        movement
                    );
            }

            await session
                .CommitTransactionAsync();
        }
        catch (InvalidOperationException exception)
        {
            await session
                .AbortTransactionAsync();

            return Conflict(
                ApiResponse<Purchase>.Fail(
                    exception.Message
                )
            );
        }
        catch
        {
            await session
                .AbortTransactionAsync();

            throw;
        }

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                id = purchase.Id
            },
            ApiResponse<Purchase>.Ok(
                purchase,
                "Compra registrada, inventario actualizado y costo promedio recalculado correctamente"
            )
        );
    }

    private PurchaseDetail BuildDetail(
        ValidatedPurchaseItem item)
    {
        return new PurchaseDetail
        {
            RawMaterialId =
                item.Material.Id,

            RawMaterialCode =
                item.Material.Code,

            RawMaterialName =
                item.Material.Name,

            UnitOfMeasureId =
                item.Unit.Id,

            UnitCode =
                item.Unit.Code,

            UnitName =
                item.Unit.SingularName,

            UnitSymbol =
                item.Unit.Symbol,

            UnitAllowsDecimals =
                item.Unit.AllowsDecimals,

            UnitDecimalPlaces =
                item.Unit.DecimalPlaces,

            Unit =
                item.Unit.Symbol,

            Quantity =
                item.Quantity,

            UnitCost =
                item.UnitCost,

            Subtotal =
                item.Subtotal,

            PreviousStock =
                item.PreviousStock,

            NewStock =
                item.NewStock,

            PreviousAverageCost =
                item.PreviousAverageCost,

            NewAverageCost =
                item.NewAverageCost
        };
    }

    private RawMaterialMovement BuildMovement(
        ValidatedPurchaseItem item,
        Supplier supplier,
        Purchase purchase)
    {
        return new RawMaterialMovement
        {
            RawMaterialId =
                item.Material.Id,

            RawMaterialCode =
                item.Material.Code,

            RawMaterialName =
                item.Material.Name,

            MovementType =
                "PurchaseEntry",

            Quantity =
                item.Quantity,

            PreviousStock =
                item.PreviousStock,

            NewStock =
                item.NewStock,

            UnitOfMeasureId =
                item.Unit.Id,

            UnitCode =
                item.Unit.Code,

            UnitName =
                item.Unit.SingularName,

            UnitSymbol =
                item.Unit.Symbol,

            UnitAllowsDecimals =
                item.Unit.AllowsDecimals,

            UnitDecimalPlaces =
                item.Unit.DecimalPlaces,

            Unit =
                item.Unit.Symbol,

            Reason =
                $"Compra {purchase.Folio} a {supplier.Name}",

            ReferenceType =
                "Purchase",

            ReferenceId =
                purchase.Id,

            UnitCost =
                item.UnitCost,

            TotalCost =
                item.Subtotal,

            MovementDate =
                purchase.PurchaseDate,

            IsDeleted =
                false,

            CreatedAt =
                DateTime.UtcNow,

            CreatedBy =
                GetCurrentUserId()
        };
    }

    private static string? ValidatePurchase(
        PurchaseCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(
                dto.SupplierId))
        {
            return
                "Debes seleccionar un proveedor";
        }

        if (dto.Details == null ||
            dto.Details.Count == 0)
        {
            return
                "La compra debe incluir al menos una materia prima";
        }

        if (dto.Details.Count > 100)
        {
            return
                "La compra no puede contener más de 100 materiales";
        }

        if (dto.Details.Any(item =>
                string.IsNullOrWhiteSpace(
                    item.RawMaterialId)))
        {
            return
                "Todos los detalles deben tener una materia prima";
        }

        if (dto.Tax < 0)
        {
            return
                "El impuesto no puede ser negativo";
        }

        if (dto.ShippingCost < 0)
        {
            return
                "El costo de envío no puede ser negativo";
        }

        if (QuantityValidationService
                .GetDecimalPlaces(dto.Tax) > 2)
        {
            return
                "El impuesto permite como máximo 2 decimales";
        }

        if (QuantityValidationService
                .GetDecimalPlaces(
                    dto.ShippingCost) > 2)
        {
            return
                "El costo de envío permite como máximo 2 decimales";
        }

        if (dto.PurchaseDate.HasValue &&
            dto.PurchaseDate.Value
                .ToUniversalTime() >
            DateTime.UtcNow.AddMinutes(5))
        {
            return
                "La fecha de compra no puede estar en el futuro";
        }

        if (dto.InvoiceNumber?.Trim().Length > 100)
        {
            return
                "El número de factura no puede superar los 100 caracteres";
        }

        if (dto.Notes?.Trim().Length > 1000)
        {
            return
                "Las observaciones no pueden superar los 1000 caracteres";
        }

        return null;
    }

    private static decimal NormalizeQuantity(
        decimal value,
        UnitOfMeasure unit)
    {
        if (!unit.AllowsDecimals)
        {
            return decimal.Truncate(value);
        }

        return decimal.Round(
            value,
            unit.DecimalPlaces,
            MidpointRounding.AwayFromZero
        );
    }

    private static string FormatQuantity(
        decimal value,
        UnitOfMeasure unit)
    {
        if (!unit.AllowsDecimals)
        {
            return decimal
                .Truncate(value)
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

        return value.ToString(format);
    }

    private static string GenerateFolio(
        DateTime date)
    {
        var suffix =
            Guid.NewGuid()
                .ToString("N")[..6]
                .ToUpperInvariant();

        return
            $"PUR-{date:yyyyMMdd-HHmmss}-{suffix}";
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private sealed class ValidatedPurchaseItem
    {
        public RawMaterial Material { get; set; } =
            new();

        public UnitOfMeasure Unit { get; set; } =
            new();

        public decimal Quantity { get; set; }

        public decimal UnitCost { get; set; }

        public decimal Subtotal { get; set; }

        public decimal PreviousStock { get; set; }

        public decimal NewStock { get; set; }

        public decimal PreviousAverageCost
        {
            get;
            set;
        }

        public decimal NewAverageCost { get; set; }
    }
}