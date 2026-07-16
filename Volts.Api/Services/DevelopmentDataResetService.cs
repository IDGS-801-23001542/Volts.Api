using Microsoft.Extensions.Options;
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
            return;

        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "ResetOnStart solamente puede utilizarse en Development."
            );
        }

        await _db.Licenses.DeleteManyAsync(
            Builders<License>.Filter.Empty
        );
        await _db.Sales.DeleteManyAsync(
            Builders<Sale>.Filter.Empty
        );
        await _db.Orders.DeleteManyAsync(
            Builders<Order>.Filter.Empty
        );
        await _db.Quotes.DeleteManyAsync(
            Builders<Quote>.Filter.Empty
        );
        await _db.CommercialPackages.DeleteManyAsync(
            Builders<CommercialPackage>.Filter.Empty
        );
        await _db.CommercialPlans.DeleteManyAsync(
            Builders<CommercialPlan>.Filter.Empty
        );
        await _db.Customers.DeleteManyAsync(
            Builders<Customer>.Filter.Empty
        );
        await _db.Institutions.DeleteManyAsync(
            Builders<Institution>.Filter.Empty
        );

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

        await _db.Comments.DeleteManyAsync(
            Builders<Comment>.Filter.Empty
        );
        await _db.ContactMessages.DeleteManyAsync(
            Builders<ContactMessage>.Filter.Empty
        );
        await _db.SupportTickets.DeleteManyAsync(
            Builders<SupportTicket>.Filter.Empty
        );
        await _db.EtlLogs.DeleteManyAsync(
            Builders<EtlLog>.Filter.Empty
        );
        await _db.AuditLogs.DeleteManyAsync(
            Builders<AuditLog>.Filter.Empty
        );
        await _db.SystemLogs.DeleteManyAsync(
            Builders<SystemLog>.Filter.Empty
        );

        /*
         * Se conserva el administrador principal.
         * Employee y todas las cuentas de portal se reconstruyen
         * con referencias ProfileId vigentes.
         */
        await _db.Users.DeleteManyAsync(
            Builders<User>.Filter.Ne(
                item => item.RoleName,
                "Admin"
            )
        );
    }
}
