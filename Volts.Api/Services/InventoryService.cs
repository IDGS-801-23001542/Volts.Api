using MongoDB.Driver;
using Volts.Api.Models;

namespace Volts.Api.Services;

public class InventoryService
{
    private readonly MongoDbService _db;

    public InventoryService(MongoDbService db)
    {
        _db = db;
    }

    public UnitOfMeasure BuildUnitSnapshot(RawMaterial material) => new()
    {
        Id = material.UnitOfMeasureId,
        Code = material.UnitCode,
        SingularName = material.UnitName,
        PluralName = material.UnitName,
        Symbol = material.UnitSymbol,
        AllowsDecimals = material.UnitAllowsDecimals,
        DecimalPlaces = material.UnitDecimalPlaces,
        IsActive = true
    };

    public decimal NormalizeQuantity(decimal value, RawMaterial material)
    {
        return material.UnitAllowsDecimals
            ? decimal.Round(value, material.UnitDecimalPlaces, MidpointRounding.AwayFromZero)
            : decimal.Truncate(value);
    }

    public async Task IssueRawMaterialAsync(
        IClientSessionHandle session,
        RawMaterial material,
        decimal quantity,
        string movementType,
        string reason,
        string referenceType,
        string referenceId,
        string? userId)
    {
        quantity = NormalizeQuantity(quantity, material);
        var previousStock = material.CurrentStock;
        var newStock = NormalizeQuantity(previousStock - quantity, material);

        if (newStock < 0)
        {
            throw new InvalidOperationException(
                $"Stock insuficiente de {material.Name}. Disponible: {previousStock} {material.UnitSymbol}."
            );
        }

        var result = await _db.RawMaterials.UpdateOneAsync(
            session,
            item => item.Id == material.Id && !item.IsDeleted && item.IsActive && item.CurrentStock == previousStock,
            Builders<RawMaterial>.Update
                .Set(item => item.CurrentStock, newStock)
                .Set(item => item.UpdatedAt, DateTime.UtcNow)
                .Set(item => item.UpdatedBy, userId)
        );

        if (result.ModifiedCount != 1)
        {
            throw new InvalidOperationException(
                $"El inventario de {material.Name} cambió durante la operación."
            );
        }

        await _db.RawMaterialMovements.InsertOneAsync(
            session,
            new RawMaterialMovement
            {
                RawMaterialId = material.Id,
                RawMaterialCode = material.Code,
                RawMaterialName = material.Name,
                MovementType = movementType,
                Quantity = quantity,
                PreviousStock = previousStock,
                NewStock = newStock,
                UnitOfMeasureId = material.UnitOfMeasureId,
                UnitCode = material.UnitCode,
                UnitName = material.UnitName,
                UnitSymbol = material.UnitSymbol,
                UnitAllowsDecimals = material.UnitAllowsDecimals,
                UnitDecimalPlaces = material.UnitDecimalPlaces,
                Unit = material.UnitSymbol,
                Reason = reason,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                UnitCost = material.AverageCost,
                TotalCost = InventoryRoundingService.RoundEstimatedCost(quantity * material.AverageCost),
                MovementDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                IsDeleted = false
            }
        );

        material.CurrentStock = newStock;
    }

    public async Task AddFinishedProductAsync(
        IClientSessionHandle session,
        Product product,
        int quantity,
        string? userId)
    {
        if (quantity < 0)
            throw new InvalidOperationException("La cantidad terminada no puede ser negativa.");

        var previousPhysical = product.PhysicalStock;
        var newPhysical = previousPhysical + quantity;

        var result = await _db.Products.UpdateOneAsync(
            session,
            item => item.Id == product.Id && !item.IsDeleted && item.PhysicalStock == previousPhysical,
            Builders<Product>.Update
                .Set(item => item.PhysicalStock, newPhysical)
                .Set(item => item.UpdatedAt, DateTime.UtcNow)
                .Set(item => item.UpdatedBy, userId)
        );

        if (result.ModifiedCount != 1)
            throw new InvalidOperationException("El stock terminado cambió durante la operación.");

        product.PhysicalStock = newPhysical;
    }
}
