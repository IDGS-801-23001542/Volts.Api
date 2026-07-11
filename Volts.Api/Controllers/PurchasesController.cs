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

    public PurchasesController(MongoDbService db)
    {
        _db = db;
    }

    // =========================================================
    // GET: api/Purchases
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var purchases = await _db.Purchases
            .Find(purchase => !purchase.IsDeleted)
            .SortByDescending(purchase => purchase.PurchaseDate)
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
        var purchases = await _db.Purchases
            .Find(purchase =>
                !purchase.IsDeleted &&
                purchase.Status == "Completed")
            .ToListAsync();

        var now = DateTime.UtcNow;

        var currentMonthPurchases = purchases
            .Where(purchase =>
                purchase.PurchaseDate.Year == now.Year &&
                purchase.PurchaseDate.Month == now.Month)
            .ToList();

        var summary = new PurchaseSummaryDto
        {
            TotalPurchases = purchases.Count,

            PurchasesThisMonth =
                currentMonthPurchases.Count,

            TotalInvested = purchases.Sum(
                purchase => purchase.Total
            ),

            InvestedThisMonth =
                currentMonthPurchases.Sum(
                    purchase => purchase.Total
                ),

            AveragePurchaseValue =
                purchases.Count == 0
                    ? 0
                    : purchases.Average(
                        purchase => purchase.Total
                    ),

            SuppliersUsed = purchases
                .Select(purchase => purchase.SupplierId)
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
    public async Task<IActionResult> GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(
                ApiResponse<Purchase>.Fail(
                    "El identificador de la compra es obligatorio"
                )
            );
        }

        var purchase = await _db.Purchases
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
    // Registra compra, actualiza inventario y costo promedio.
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] PurchaseCreateDto dto)
    {
        var validationError = ValidatePurchase(dto);

        if (validationError != null)
        {
            return BadRequest(
                ApiResponse<Purchase>.Fail(
                    validationError
                )
            );
        }

        var supplier = await _db.Suppliers
            .Find(item =>
                item.Id == dto.SupplierId &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (supplier == null)
        {
            return BadRequest(
                ApiResponse<Purchase>.Fail(
                    "Proveedor no encontrado"
                )
            );
        }

        if (!supplier.IsActive)
        {
            return BadRequest(
                ApiResponse<Purchase>.Fail(
                    "No se pueden registrar compras con un proveedor inactivo"
                )
            );
        }

        var repeatedMaterialId = dto.Details
            .GroupBy(detail => detail.RawMaterialId)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (repeatedMaterialId != null)
        {
            return BadRequest(
                ApiResponse<Purchase>.Fail(
                    "Una materia prima no puede aparecer más de una vez en la misma compra"
                )
            );
        }

        var validatedItems = new List<ValidatedPurchaseItem>();

        foreach (var item in dto.Details)
        {
            var material = await _db.RawMaterials
                .Find(rawMaterial =>
                    rawMaterial.Id == item.RawMaterialId &&
                    !rawMaterial.IsDeleted)
                .FirstOrDefaultAsync();

            if (material == null)
            {
                return BadRequest(
                    ApiResponse<Purchase>.Fail(
                        $"Materia prima no encontrada: {item.RawMaterialId}"
                    )
                );
            }

            if (!material.IsActive)
            {
                return BadRequest(
                    ApiResponse<Purchase>.Fail(
                        $"La materia prima {material.Name} está inactiva"
                    )
                );
            }

            if (item.Quantity <= 0)
            {
                return BadRequest(
                    ApiResponse<Purchase>.Fail(
                        $"La cantidad de {material.Name} debe ser mayor a cero"
                    )
                );
            }

            if (item.UnitCost < 0)
            {
                return BadRequest(
                    ApiResponse<Purchase>.Fail(
                        $"El costo de {material.Name} no puede ser negativo"
                    )
                );
            }

            var newStock =
                material.CurrentStock + item.Quantity;

            if (material.MaximumStock > 0 &&
                newStock > material.MaximumStock)
            {
                return BadRequest(
                    ApiResponse<Purchase>.Fail(
                        $"La compra de {material.Name} superaría " +
                        $"el stock máximo de {material.MaximumStock} " +
                        $"{material.Unit}. Stock actual: " +
                        $"{material.CurrentStock} {material.Unit}"
                    )
                );
            }

            validatedItems.Add(
                new ValidatedPurchaseItem
                {
                    Material = material,
                    Quantity = item.Quantity,
                    UnitCost = item.UnitCost
                }
            );
        }

        var purchaseDate =
            dto.PurchaseDate?.ToUniversalTime()
            ?? DateTime.UtcNow;

        var folio = await GenerateFolioAsync(
            purchaseDate
        );

        var details = new List<PurchaseDetail>();

        decimal subtotal = 0;

        foreach (var item in validatedItems)
        {
            var material = item.Material;

            var detailSubtotal =
                item.Quantity * item.UnitCost;

            var previousStock =
                material.CurrentStock;

            var previousAverageCost =
                material.AverageCost;

            var newStock =
                previousStock + item.Quantity;

            var previousInventoryValue =
                previousStock *
                previousAverageCost;

            var purchasedValue =
                item.Quantity *
                item.UnitCost;

            var newAverageCost =
                newStock <= 0
                    ? item.UnitCost
                    : (
                        previousInventoryValue +
                        purchasedValue
                    ) / newStock;

            details.Add(
                new PurchaseDetail
                {
                    RawMaterialId = material.Id,
                    RawMaterialCode = material.Code,
                    RawMaterialName = material.Name,
                    Unit = material.Unit,
                    Quantity = item.Quantity,
                    UnitCost = item.UnitCost,
                    Subtotal = detailSubtotal,
                    PreviousStock = previousStock,
                    NewStock = newStock,
                    PreviousAverageCost =
                        previousAverageCost,
                    NewAverageCost =
                        newAverageCost
                }
            );

            subtotal += detailSubtotal;
        }

        var total =
            subtotal +
            dto.Tax +
            dto.ShippingCost;

        var purchase = new Purchase
        {
            Folio = folio,

            InvoiceNumber =
                NormalizeOptional(
                    dto.InvoiceNumber
                ),

            SupplierId = supplier.Id,
            SupplierCode = supplier.Code,
            SupplierName = supplier.Name,

            PurchaseDate = purchaseDate,

            Subtotal = subtotal,
            Tax = dto.Tax,
            ShippingCost = dto.ShippingCost,
            Total = total,

            Status = "Completed",

            Notes = dto.Notes.Trim(),

            Details = details,

            CreatedAt = DateTime.UtcNow,
            CreatedBy = GetCurrentUserId(),

            IsDeleted = false
        };

        await _db.Purchases.InsertOneAsync(purchase);

        foreach (var detail in purchase.Details)
        {
            var materialUpdate =
                Builders<RawMaterial>.Update
                    .Set(
                        material =>
                            material.CurrentStock,
                        detail.NewStock
                    )
                    .Set(
                        material =>
                            material.AverageCost,
                        detail.NewAverageCost
                    )
                    .Set(
                        material =>
                            material.LastPurchaseCost,
                        detail.UnitCost
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

            await _db.RawMaterials.UpdateOneAsync(
                material =>
                    material.Id ==
                    detail.RawMaterialId &&
                    !material.IsDeleted,
                materialUpdate
            );

            var movement = new RawMaterialMovement
            {
                RawMaterialId =
                    detail.RawMaterialId,

                RawMaterialCode =
                    detail.RawMaterialCode,

                RawMaterialName =
                    detail.RawMaterialName,

                MovementType = "Purchase",

                Quantity = detail.Quantity,

                PreviousStock =
                    detail.PreviousStock,

                NewStock = detail.NewStock,

                Unit = detail.Unit,

                Reason =
                    $"Compra {purchase.Folio} a " +
                    $"{supplier.Name}",

                ReferenceType = "Purchase",

                ReferenceId = purchase.Id,

                UnitCost = detail.UnitCost,

                TotalCost = detail.Subtotal,

                MovementDate = purchase.PurchaseDate,

                CreatedAt = DateTime.UtcNow,

                CreatedBy = GetCurrentUserId(),

                IsDeleted = false
            };

            await _db.RawMaterialMovements
                .InsertOneAsync(movement);
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = purchase.Id },
            ApiResponse<Purchase>.Ok(
                purchase,
                "Compra registrada, inventario actualizado y costo promedio recalculado correctamente"
            )
        );
    }

    private static string? ValidatePurchase(
        PurchaseCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(
            dto.SupplierId))
        {
            return "Debes seleccionar un proveedor";
        }

        if (dto.Details == null ||
            dto.Details.Count == 0)
        {
            return "La compra debe incluir al menos una materia prima";
        }

        if (dto.Details.Count > 100)
        {
            return "La compra no puede contener más de 100 materiales";
        }

        if (dto.Tax < 0)
        {
            return "El impuesto no puede ser negativo";
        }

        if (dto.ShippingCost < 0)
        {
            return "El costo de envío no puede ser negativo";
        }

        if (dto.InvoiceNumber?.Trim().Length > 100)
        {
            return "El número de factura no puede superar los 100 caracteres";
        }

        if (dto.Notes.Trim().Length > 1000)
        {
            return "Las observaciones no pueden superar los 1000 caracteres";
        }

        return null;
    }

    private async Task<string> GenerateFolioAsync(
        DateTime purchaseDate)
    {
        var prefix =
            $"PUR-{purchaseDate:yyyyMMdd}";

        var startDate =
            purchaseDate.Date;

        var endDate =
            startDate.AddDays(1);

        var purchasesToday =
            await _db.Purchases.CountDocumentsAsync(
                purchase =>
                    purchase.PurchaseDate >= startDate &&
                    purchase.PurchaseDate < endDate
            );

        var consecutive =
            purchasesToday + 1;

        var folio =
            $"{prefix}-{consecutive:0000}";

        var exists = await _db.Purchases
            .Find(purchase =>
                purchase.Folio == folio)
            .AnyAsync();

        while (exists)
        {
            consecutive++;

            folio =
                $"{prefix}-{consecutive:0000}";

            exists = await _db.Purchases
                .Find(purchase =>
                    purchase.Folio == folio)
                .AnyAsync();
        }

        return folio;
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

        public decimal Quantity { get; set; }

        public decimal UnitCost { get; set; }
    }
}