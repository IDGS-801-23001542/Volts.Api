using MongoDB.Driver;
using Volts.Api.Models;
using Volts.Api.Models.Enums;

namespace Volts.Api.Services;

public class ProductionInventorySeedService
{
    private const string SeedUser = "SystemSeed";
    private readonly MongoDbService _db;

    public ProductionInventorySeedService(MongoDbService db)
    {
        _db = db;
    }

    public async Task SeedAsync()
    {
        var products = await _db.Products
            .Find(x => !x.IsDeleted && x.IsActive && x.CanBeProduced)
            .ToListAsync();

        var materials = await _db.RawMaterials
            .Find(x => !x.IsDeleted && x.IsActive)
            .ToListAsync();

        var byCode = materials.ToDictionary(
            x => x.Code,
            StringComparer.OrdinalIgnoreCase
        );

        ValidateRequiredMaterials(byCode);

        foreach (var product in products)
        {
            var exists = await _db.Recipes
                .Find(x =>
                    x.ProductId == product.Id &&
                    x.Version == 1 &&
                    !x.IsDeleted)
                .AnyAsync();

            if (exists)
            {
                continue;
            }

            await _db.Recipes.InsertOneAsync(
                BuildRecipe(product, byCode)
            );
        }
    }

    private static Recipe BuildRecipe(
        Product product,
        IReadOnlyDictionary<string, RawMaterial> byCode)
    {
        var details = new List<RecipeDetail>
        {
            Detail(byCode["RAW-ELE-001"], 1m, 0m),
            Detail(byCode["RAW-MEC-001"], 4m, 0m),
            Detail(byCode["RAW-ELE-002"], 1m, 0m),
            Detail(byCode["RAW-ELE-003"], 1m, 0m),
            Detail(byCode["RAW-ELE-004"], 1m, 0m),
            Detail(byCode["RAW-ELE-005"], 1m, 0m),
            Detail(byCode["RAW-ELE-006"], 3m, 0m),
            Detail(byCode["RAW-ELE-008"], 1m, 0m),
            Detail(byCode["RAW-ELE-009"], 12m, 0m),
            Detail(byCode["RAW-MEC-002"], 0.12m, 5m),
            Detail(byCode["RAW-CAR-001"], 0.35m, 10m),
            Detail(byCode["RAW-ADH-001"], 20m, 5m),
            Detail(byCode["RAW-MAD-001"], 4m, 0m),
            Detail(byCode["RAW-MAD-002"], 4m, 0m),
            Detail(byCode["RAW-PNT-001"], 4m, 5m),
            Detail(byCode["RAW-PNT-002"], 2m, 5m),
            Detail(byCode["RAW-PNT-003"], 2m, 5m),
            Detail(byCode["RAW-ADH-002"], 0.50m, 5m)
        };

        AddFabricDetails(product.Slug, details, byCode);

        var recipe = new Recipe
        {
            Code = $"REC-{product.Slug.ToUpperInvariant()}-V1",
            ProductId = product.Id,
            ProductName = product.Name,
            Version = 1,
            Status = RecipeStatus.Active,
            IsActive = true,
            Notes = "Receta BOM base definitiva de VOLTS.",
            Details = details,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = SeedUser
        };

        recipe.EstimatedUnitCost =
            InventoryRoundingService.RoundEstimatedCost(
                recipe.Details.Sum(x => x.EstimatedSubtotal)
            );

        return recipe;
    }

    private static void AddFabricDetails(
        string productSlug,
        ICollection<RecipeDetail> details,
        IReadOnlyDictionary<string, RawMaterial> byCode)
    {
        switch (productSlug)
        {
            case "volts-husky":
                details.Add(Detail(byCode["RAW-TEX-001"], 0.12m, 8m));
                details.Add(Detail(byCode["RAW-TEX-002"], 0.08m, 8m));
                break;

            case "volts-rottweiler":
                details.Add(Detail(byCode["RAW-TEX-003"], 0.14m, 8m));
                details.Add(Detail(byCode["RAW-TEX-004"], 0.06m, 8m));
                break;

            case "volts-caramelo":
                details.Add(Detail(byCode["RAW-TEX-004"], 0.20m, 8m));
                break;

            default:
                details.Add(Detail(byCode["RAW-TEX-004"], 0.20m, 8m));
                break;
        }
    }

    private static RecipeDetail Detail(
        RawMaterial material,
        decimal quantity,
        decimal wastePercentage)
    {
        ValidateQuantity(material, quantity);

        if (wastePercentage < 0m || wastePercentage > 100m)
        {
            throw new InvalidOperationException(
                $"La merma de {material.Name} debe estar entre 0 y 100."
            );
        }

        var total = decimal.Round(
            quantity * (1m + wastePercentage / 100m),
            material.UnitDecimalPlaces,
            MidpointRounding.AwayFromZero
        );

        if (!material.UnitAllowsDecimals && decimal.Truncate(total) != total)
        {
            throw new InvalidOperationException(
                $"La merma genera una fracción inválida de {material.Name}."
            );
        }

        return new RecipeDetail
        {
            RawMaterialId = material.Id,
            RawMaterialCode = material.Code,
            RawMaterialName = material.Name,
            UnitOfMeasureId = material.UnitOfMeasureId,
            UnitCode = material.UnitCode,
            UnitName = material.UnitName,
            UnitSymbol = material.UnitSymbol,
            UnitAllowsDecimals = material.UnitAllowsDecimals,
            UnitDecimalPlaces = material.UnitDecimalPlaces,
            Unit = material.UnitSymbol,
            QuantityRequired = quantity,
            WastePercentage = wastePercentage,
            TotalQuantityPerUnit = total,
            AcceptsRecoveredWaste = material.IsReusable,
            EstimatedUnitCost = material.AverageCost,
            EstimatedSubtotal =
                InventoryRoundingService.RoundEstimatedCost(
                    total * material.AverageCost
                )
        };
    }

    private static void ValidateRequiredMaterials(
        IReadOnlyDictionary<string, RawMaterial> byCode)
    {
        var requiredCodes = new[]
        {
            "RAW-ELE-001", "RAW-MEC-001", "RAW-ELE-002",
            "RAW-ELE-003", "RAW-ELE-004", "RAW-ELE-005",
            "RAW-ELE-006", "RAW-ELE-008", "RAW-ELE-009",
            "RAW-MEC-002", "RAW-CAR-001", "RAW-ADH-001",
            "RAW-TEX-001", "RAW-TEX-002", "RAW-TEX-003",
            "RAW-TEX-004", "RAW-MAD-001", "RAW-MAD-002",
            "RAW-PNT-001", "RAW-PNT-002", "RAW-PNT-003",
            "RAW-ADH-002"
        };

        var missing = requiredCodes
            .Where(code => !byCode.ContainsKey(code))
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Faltan materias primas para las recetas: " +
                string.Join(", ", missing)
            );
        }
    }

    private static void ValidateQuantity(
        RawMaterial material,
        decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException(
                $"La cantidad de {material.Name} debe ser mayor a cero."
            );
        }

        if (!material.UnitAllowsDecimals && decimal.Truncate(quantity) != quantity)
        {
            throw new InvalidOperationException(
                $"{material.Name} utiliza {material.UnitSymbol} y no acepta decimales."
            );
        }

        if (
            decimal.Round(
                quantity,
                material.UnitDecimalPlaces,
                MidpointRounding.AwayFromZero
            ) != quantity
        )
        {
            throw new InvalidOperationException(
                $"{material.Name} acepta como máximo " +
                $"{material.UnitDecimalPlaces} decimales."
            );
        }
    }
}