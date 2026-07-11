using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Volts.Api.Models;
using Volts.Api.Settings;

namespace Volts.Api.Services;

public class MongoDbService
{
    private readonly MongoClient _client;
    private readonly IMongoDatabase _database;

    public MongoDbService(
        IOptions<MongoDbSettings> settings)
    {
        _client = new MongoClient(
            settings.Value.ConnectionString
        );

        _database = _client.GetDatabase(
            settings.Value.DatabaseName
        );
    }

    public Task<IClientSessionHandle> StartSessionAsync()
    {
        return _client.StartSessionAsync();
    }

    public IMongoCollection<User> Users =>
        _database.GetCollection<User>("Users");

    public IMongoCollection<Role> Roles =>
        _database.GetCollection<Role>("Roles");

    public IMongoCollection<Customer> Customers =>
        _database.GetCollection<Customer>("Customers");

    public IMongoCollection<Product> Products =>
        _database.GetCollection<Product>("Products");

    public IMongoCollection<ContactMessage> ContactMessages =>
        _database.GetCollection<ContactMessage>(
            "ContactMessages"
        );

    public IMongoCollection<Quote> Quotes =>
        _database.GetCollection<Quote>("Quotes");

    public IMongoCollection<Institution> Institutions =>
        _database.GetCollection<Institution>(
            "Institutions"
        );

    public IMongoCollection<License> Licenses =>
        _database.GetCollection<License>("Licenses");

    public IMongoCollection<Category> Categories =>
        _database.GetCollection<Category>("Categories");

    public IMongoCollection<Supplier> Suppliers =>
        _database.GetCollection<Supplier>("Suppliers");

    public IMongoCollection<RawMaterial> RawMaterials =>
        _database.GetCollection<RawMaterial>(
            "RawMaterials"
        );

    public IMongoCollection<RawMaterialMovement>
        RawMaterialMovements =>
            _database.GetCollection<RawMaterialMovement>(
                "RawMaterialMovements"
            );

    public IMongoCollection<Purchase> Purchases =>
        _database.GetCollection<Purchase>("Purchases");

    public IMongoCollection<Recipe> Recipes =>
        _database.GetCollection<Recipe>("Recipes");

    public IMongoCollection<ProductionOrder>
        ProductionOrders =>
            _database.GetCollection<ProductionOrder>(
                "ProductionOrders"
            );

    public IMongoCollection<Waste> Wastes =>
        _database.GetCollection<Waste>("Wastes");

    public IMongoCollection<Order> Orders =>
        _database.GetCollection<Order>("Orders");

    public IMongoCollection<Sale> Sales =>
        _database.GetCollection<Sale>("Sales");

    public IMongoCollection<AuditLog> AuditLogs =>
        _database.GetCollection<AuditLog>(
            "AuditLogs"
        );

    public IMongoCollection<SystemLog> SystemLogs =>
        _database.GetCollection<SystemLog>(
            "SystemLogs"
        );

    public IMongoCollection<Documentation> Documentation =>
        _database.GetCollection<Documentation>(
            "Documentation"
        );

    public IMongoCollection<SupportTicket> SupportTickets =>
        _database.GetCollection<SupportTicket>(
            "SupportTickets"
        );

    public IMongoCollection<UpdateNews> UpdateNews =>
        _database.GetCollection<UpdateNews>(
            "UpdateNews"
        );

    public IMongoCollection<Notification> Notifications =>
        _database.GetCollection<Notification>(
            "Notifications"
        );

    public IMongoCollection<EtlLog> EtlLogs =>
        _database.GetCollection<EtlLog>("EtlLogs");

    public IMongoCollection<Comment> Comments =>
        _database.GetCollection<Comment>("Comments");
}