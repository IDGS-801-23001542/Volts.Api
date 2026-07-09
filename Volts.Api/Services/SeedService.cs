using MongoDB.Driver;
using Volts.Api.Models;

namespace Volts.Api.Services;

public class SeedService
{
    private readonly MongoDbService _db;

    public SeedService(MongoDbService db)
    {
        _db = db;
    }

    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedAdminAsync();
        await SeedProductsAsync();
    }

    private async Task SeedRolesAsync()
    {
        var roles = new List<Role>
        {
            new()
            {
                Name = "Admin",
                Description = "Administrador general del sistema",
                Permissions = new List<string> { "*" }
            },
            new()
            {
                Name = "Employee",
                Description = "Empleado con permisos limitados",
                Permissions = new List<string>
                {
                    "customers.read",
                    "quotes.read",
                    "quotes.update",
                    "products.read",
                    "contacts.read"
                }
            },
            new()
            {
                Name = "Client",
                Description = "Cliente del portal VOLTS",
                Permissions = new List<string>
                {
                    "profile.read",
                    "quotes.create",
                    "licenses.read",
                    "documents.read"
                }
            }
        };

        foreach (var role in roles)
        {
            var exists = await _db.Roles.Find(x => x.Name == role.Name).AnyAsync();

            if (!exists)
                await _db.Roles.InsertOneAsync(role);
        }
    }

    private async Task SeedAdminAsync()
    {
        var exists = await _db.Users
            .Find(x => x.Email == "voltsidgs@gmail.com")
            .AnyAsync();

        if (exists)
            return;

        var adminRole = await _db.Roles
            .Find(x => x.Name == "Admin")
            .FirstOrDefaultAsync();

        if (adminRole == null)
            return;

        var admin = new User
        {
            FullName = "Administrador VOLTS",
            Email = "voltsidgs@gmail.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            RoleId = adminRole.Id,
            RoleName = adminRole.Name,
            IsActive = true,
            TwoFactorEnabled = false
        };

        await _db.Users.InsertOneAsync(admin);
    }

    private async Task SeedProductsAsync()
    {
        var exists = await _db.Products.Find(x => true).AnyAsync();

        if (exists)
            return;

        var products = new List<Product>
        {
            new()
            {
                Name = "VOLTS Estándar",
                Slug = "volts-estandar",
                Description = "Perro robot ecológico educativo con funciones básicas de interacción.",
                Price = 349,
                Category = "Robot educativo"
            },
            new()
            {
                Name = "VOLTS Pro",
                Slug = "volts-pro",
                Description = "Versión avanzada para docentes, escuelas e instituciones educativas.",
                Price = 549,
                Category = "Robot educativo"
            }
        };

        await _db.Products.InsertManyAsync(products);
    }
}