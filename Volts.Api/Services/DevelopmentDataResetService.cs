using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Volts.Api.Models;
using Volts.Api.Settings;

namespace Volts.Api.Services;

public class DevelopmentDataResetService
{
    private readonly MongoDbService _db;
    private readonly IWebHostEnvironment _environment;
    private readonly SeedSettings _settings;

    public DevelopmentDataResetService(
        MongoDbService db,
        IWebHostEnvironment environment,
        IOptions<SeedSettings> settings)
    {
        _db = db;
        _environment = environment;
        _settings = settings.Value;
    }

    public async Task ResetAsync()
    {
        if (!_settings.ResetOnStart)
        {
            return;
        }

        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "ResetOnStart solamente puede utilizarse en Development."
            );
        }

        await _db.RawMaterialMovements.DeleteManyAsync(
            Builders<RawMaterialMovement>.Filter.Empty
        );

        await _db.Wastes.DeleteManyAsync(
            Builders<Waste>.Filter.Empty
        );

        await _db.ProductionOrders.DeleteManyAsync(
            Builders<ProductionOrder>.Filter.Empty
        );

        await _db.Recipes.DeleteManyAsync(
            Builders<Recipe>.Filter.Empty
        );

        await _db.Purchases.DeleteManyAsync(
            Builders<Purchase>.Filter.Empty
        );

        await _db.RawMaterials.DeleteManyAsync(
            Builders<RawMaterial>.Filter.Empty
        );

        await _db.Suppliers.DeleteManyAsync(
            Builders<Supplier>.Filter.Empty
        );

        await _db.Products.DeleteManyAsync(
            Builders<Product>.Filter.Empty
        );

        await _db.Categories.DeleteManyAsync(
            Builders<Category>.Filter.Empty
        );

        await _db.UnitsOfMeasure.DeleteManyAsync(
            Builders<UnitOfMeasure>.Filter.Empty
        );

        /*
         * El administrador se conserva.
         * Solo se elimina el empleado semilla para recrearlo
         * con el modelo estructurado de nombres.
         */
        var employeeEmailFilter =
            Builders<User>.Filter.Regex(
                user => user.Email,
                new BsonRegularExpression(
                    "^voltsempleado@gmail\\.com$",
                    "i"
                )
            );

        await _db.Users.DeleteManyAsync(
            employeeEmailFilter
        );
    }
}
