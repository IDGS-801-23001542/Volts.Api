using MongoDB.Driver;
using Volts.Api.Models;

namespace Volts.Api.Services;

public class SeedService
{
    private readonly MongoDbService _db;
    private readonly IConfiguration _configuration;

    public SeedService(
        MongoDbService db,
        IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedAdminAsync();
        await SeedRawMaterialsAsync();
    }

    private async Task SeedRolesAsync()
    {
        var roles = new List<Role>
        {
            new()
            {
                Name = "Admin",
                Description =
                    "Administrador general del sistema",
                Permissions = new List<string>
                {
                    "*"
                },
                IsActive = true
            },
            new()
            {
                Name = "Employee",
                Description =
                    "Empleado con acceso operativo al backoffice",
                Permissions = new List<string>
                {
                    "dashboard.read",

                    "customers.read",
                    "customers.create",
                    "customers.update",

                    "institutions.read",
                    "institutions.create",
                    "institutions.update",

                    "quotes.read",
                    "quotes.update",

                    "orders.read",
                    "orders.create",
                    "orders.update",

                    "sales.read",
                    "sales.create",

                    "products.read",

                    "categories.read",

                    "raw-materials.read",
                    "raw-materials.create",
                    "raw-materials.update",
                    "raw-materials.stock",

                    "suppliers.read",
                    "suppliers.create",
                    "suppliers.update",

                    "purchases.read",
                    "purchases.create",

                    "recipes.read",
                    "recipes.create",
                    "recipes.update",

                    "production.read",
                    "production.create",
                    "production.update",

                    "waste.read",
                    "waste.create",

                    "comments.read",
                    "comments.update",

                    "contacts.read",

                    "support.read",
                    "support.update",

                    "documentation.read",
                    "updates.read",
                    "notifications.read"
                },
                IsActive = true
            },
            new()
            {
                Name = "Client",
                Description =
                    "Cliente del portal VOLTS",
                Permissions = new List<string>
                {
                    "profile.read",
                    "profile.update",

                    "quotes.create",
                    "quotes.read-own",

                    "orders.read-own",
                    "purchases.read-own",

                    "licenses.read-own",
                    "products.read",

                    "documents.read",
                    "comments.create",
                    "support.create",
                    "support.read-own",
                    "notifications.read-own"
                },
                IsActive = true
            }
        };

        foreach (var roleToSeed in roles)
        {
            var existingRole = await _db.Roles
                .Find(role =>
                    role.Name == roleToSeed.Name &&
                    !role.IsDeleted)
                .FirstOrDefaultAsync();

            if (existingRole == null)
            {
                await _db.Roles.InsertOneAsync(
                    roleToSeed
                );

                continue;
            }

            var update = Builders<Role>.Update
                .Set(
                    role => role.Description,
                    roleToSeed.Description
                )
                .Set(
                    role => role.Permissions,
                    roleToSeed.Permissions
                )
                .Set(
                    role => role.IsActive,
                    true
                )
                .Set(
                    role => role.UpdatedAt,
                    DateTime.UtcNow
                );

            await _db.Roles.UpdateOneAsync(
                role => role.Id == existingRole.Id,
                update
            );
        }
    }

    private async Task SeedAdminAsync()
    {
        const string adminEmail =
            "voltsidgs@gmail.com";

        var exists = await _db.Users
            .Find(user =>
                user.Email == adminEmail &&
                !user.IsDeleted)
            .AnyAsync();

        if (exists)
            return;

        var adminRole = await _db.Roles
            .Find(role =>
                role.Name == "Admin" &&
                !role.IsDeleted &&
                role.IsActive)
            .FirstOrDefaultAsync();

        if (adminRole == null)
            return;

        /*
         * Puedes definir VOLTS_ADMIN_PASSWORD como
         * variable de entorno.
         *
         * En desarrollo, usa temporalmente Admin123!.
         */
        var adminPassword =
            Environment.GetEnvironmentVariable(
                "VOLTS_ADMIN_PASSWORD"
            )
            ??
            _configuration[
                "SeedSettings:AdminPassword"
            ]
            ??
            "Admin123!";

        var admin = new User
        {
            FullName = "Administrador VOLTS",
            Email = adminEmail,
            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    adminPassword
                ),
            RoleId = adminRole.Id,
            RoleName = adminRole.Name,
            IsActive = true,
            TwoFactorEnabled = false,
            CreatedAt = DateTime.UtcNow
        };

        await _db.Users.InsertOneAsync(admin);
    }

    private async Task SeedRawMaterialsAsync()
    {
        var materials = new List<RawMaterial>
        {
            CreateMaterial(
                code: "RAW-CAR-001",
                name: "Cartón corrugado grueso",
                description:
                    "Cartón utilizado para fabricar el cuerpo estructural del perro VOLTS.",
                category: "Cardboard",
                unit: "Lámina",
                minimumStock: 5,
                maximumStock: 100,
                isRecycled: true,
                isReusable: true,
                requiresPurchase: false,
                storageLocation: "A-01"
            ),

            CreateMaterial(
                code: "RAW-CAR-002",
                name: "Cartón ligero",
                description:
                    "Cartón flexible utilizado en la cabeza, cara, cola y lomo de VOLTS.",
                category: "Cardboard",
                unit: "Lámina",
                minimumStock: 5,
                maximumStock: 100,
                isRecycled: true,
                isReusable: true,
                requiresPurchase: false,
                storageLocation: "A-02"
            ),

            CreateMaterial(
                code: "RAW-ELE-001",
                name: "ESP32-WROOM DevKit",
                description:
                    "Microcontrolador principal con conectividad Bluetooth y Wi-Fi.",
                category: "Electronics",
                unit: "Pieza",
                minimumStock: 3,
                maximumStock: 50,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "B-01"
            ),

            CreateMaterial(
                code: "RAW-ELE-002",
                name: "Servomotor SG90",
                description:
                    "Microservomotor utilizado para el movimiento de las cuatro patas.",
                category: "Electronics",
                unit: "Pieza",
                minimumStock: 12,
                maximumStock: 200,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "B-02"
            ),

            CreateMaterial(
                code: "RAW-ELE-003",
                name: "LED RGB difuso",
                description:
                    "LED RGB utilizado para representar el estado emocional de VOLTS.",
                category: "Electronics",
                unit: "Pieza",
                minimumStock: 5,
                maximumStock: 100,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "B-03"
            ),

            CreateMaterial(
                code: "RAW-ELE-004",
                name: "Push button",
                description:
                    "Botón momentáneo utilizado para interacción o activación de funciones.",
                category: "Electronics",
                unit: "Pieza",
                minimumStock: 5,
                maximumStock: 100,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "B-04"
            ),

            CreateMaterial(
                code: "RAW-ELE-005",
                name: "Switch ON/OFF",
                description:
                    "Interruptor utilizado para encender y apagar el sistema.",
                category: "Electronics",
                unit: "Pieza",
                minimumStock: 5,
                maximumStock: 100,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "B-05"
            ),

            CreateMaterial(
                code: "RAW-ELE-006",
                name: "Portapilas para 4 pilas AA",
                description:
                    "Portapilas utilizado como fuente principal de energía del prototipo.",
                category: "Electronics",
                unit: "Pieza",
                minimumStock: 3,
                maximumStock: 50,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "B-06"
            ),

            CreateMaterial(
                code: "RAW-ELE-007",
                name: "Pila AA",
                description:
                    "Pila AA utilizada para alimentar el prototipo VOLTS.",
                category: "Electronics",
                unit: "Pieza",
                minimumStock: 20,
                maximumStock: 300,
                isRecycled: false,
                isReusable: false,
                requiresPurchase: true,
                storageLocation: "B-07"
            ),

            CreateMaterial(
                code: "RAW-ELE-008",
                name: "Resistencia 220 ohms",
                description:
                    "Resistencia usada para limitar corriente en los canales del LED RGB.",
                category: "Electronics",
                unit: "Pieza",
                minimumStock: 20,
                maximumStock: 500,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "B-08"
            ),

            CreateMaterial(
                code: "RAW-ELE-009",
                name: "Resistencia 10 kiloohms",
                description:
                    "Resistencia utilizada como pull-up o pull-down para botones y señales.",
                category: "Electronics",
                unit: "Pieza",
                minimumStock: 10,
                maximumStock: 500,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "B-09"
            ),

            CreateMaterial(
                code: "RAW-ELE-010",
                name: "Resistencia 47 kiloohms",
                description:
                    "Resistencia considerada para el divisor de voltaje de medición de batería.",
                category: "Electronics",
                unit: "Pieza",
                minimumStock: 10,
                maximumStock: 500,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "B-10"
            ),

            CreateMaterial(
                code: "RAW-ELE-011",
                name: "Resistencia 100 kiloohms",
                description:
                    "Resistencia considerada para el divisor de voltaje de medición de batería.",
                category: "Electronics",
                unit: "Pieza",
                minimumStock: 10,
                maximumStock: 500,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "B-11"
            ),

            CreateMaterial(
                code: "RAW-ELE-012",
                name: "Cable Dupont",
                description:
                    "Cable utilizado para conexiones internas entre el ESP32, sensores y actuadores.",
                category: "Electronics",
                unit: "Pieza",
                minimumStock: 30,
                maximumStock: 500,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "B-12"
            ),

            CreateMaterial(
                code: "RAW-ELE-013",
                name: "Placa perforada PCB",
                description:
                    "Placa empleada para organizar y soldar componentes electrónicos.",
                category: "Electronics",
                unit: "Pieza",
                minimumStock: 3,
                maximumStock: 50,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "B-13"
            ),

            CreateMaterial(
                code: "RAW-ELE-014",
                name: "Regulador DC-DC de 5 V",
                description:
                    "Convertidor de voltaje para regular la alimentación del circuito a 5 V.",
                category: "Electronics",
                unit: "Pieza",
                minimumStock: 3,
                maximumStock: 50,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "B-14"
            ),

            CreateMaterial(
                code: "RAW-MEC-001",
                name: "Palito de madera para extensión",
                description:
                    "Palito utilizado como extensión mecánica de las patas.",
                category: "Mechanical",
                unit: "Pieza",
                minimumStock: 20,
                maximumStock: 300,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "C-01"
            ),

            CreateMaterial(
                code: "RAW-MEC-002",
                name: "Palito de paleta",
                description:
                    "Ice cream stick utilizado como soporte y refuerzo estructural.",
                category: "Mechanical",
                unit: "Pieza",
                minimumStock: 20,
                maximumStock: 300,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "C-02"
            ),

            CreateMaterial(
                code: "RAW-TEX-001",
                name: "Tela de peluche",
                description:
                    "Tela utilizada para recubrir el cuerpo del perro VOLTS.",
                category: "Textiles",
                unit: "Metro cuadrado",
                minimumStock: 2,
                maximumStock: 50,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "D-01"
            ),

            CreateMaterial(
                code: "RAW-ADH-001",
                name: "Silicón en barra",
                description:
                    "Adhesivo térmico utilizado para unir cartón, tela y elementos estructurales.",
                category: "Adhesives",
                unit: "Barra",
                minimumStock: 10,
                maximumStock: 200,
                isRecycled: false,
                isReusable: false,
                requiresPurchase: true,
                storageLocation: "E-01"
            ),

            CreateMaterial(
                code: "RAW-ADH-002",
                name: "Plastiloka",
                description:
                    "Masilla epóxica empleada para fijaciones y refuerzos de mayor resistencia.",
                category: "Adhesives",
                unit: "Paquete",
                minimumStock: 3,
                maximumStock: 50,
                isRecycled: false,
                isReusable: false,
                requiresPurchase: true,
                storageLocation: "E-02"
            ),

            CreateMaterial(
                code: "RAW-ADH-003",
                name: "Kolaloka",
                description:
                    "Adhesivo instantáneo utilizado en piezas pequeñas y uniones puntuales.",
                category: "Adhesives",
                unit: "Pieza",
                minimumStock: 3,
                maximumStock: 50,
                isRecycled: false,
                isReusable: false,
                requiresPurchase: true,
                storageLocation: "E-03"
            ),

            CreateMaterial(
                code: "RAW-SOL-001",
                name: "Alambre para soldadura",
                description:
                    "Cable conductor utilizado en conexiones soldadas internas.",
                category: "Soldering",
                unit: "Metro",
                minimumStock: 5,
                maximumStock: 100,
                isRecycled: false,
                isReusable: true,
                requiresPurchase: true,
                storageLocation: "F-01"
            ),

            CreateMaterial(
                code: "RAW-SOL-002",
                name: "Estaño para soldar",
                description:
                    "Material consumible utilizado para realizar uniones eléctricas.",
                category: "Soldering",
                unit: "Gramo",
                minimumStock: 50,
                maximumStock: 1000,
                isRecycled: false,
                isReusable: false,
                requiresPurchase: true,
                storageLocation: "F-02"
            ),

            CreateMaterial(
                code: "RAW-SOL-003",
                name: "Flux para soldadura",
                description:
                    "Material auxiliar utilizado para mejorar la adherencia del estaño.",
                category: "Soldering",
                unit: "Mililitro",
                minimumStock: 20,
                maximumStock: 500,
                isRecycled: false,
                isReusable: false,
                requiresPurchase: true,
                storageLocation: "F-03"
            )
        };

        foreach (var material in materials)
        {
            var exists = await _db.RawMaterials
                .Find(existing =>
                    existing.Code == material.Code &&
                    !existing.IsDeleted)
                .AnyAsync();

            if (!exists)
            {
                await _db.RawMaterials
                    .InsertOneAsync(material);
            }
        }
    }

    private static RawMaterial CreateMaterial(
        string code,
        string name,
        string description,
        string category,
        string unit,
        decimal minimumStock,
        decimal maximumStock,
        bool isRecycled,
        bool isReusable,
        bool requiresPurchase,
        string storageLocation)
    {
        return new RawMaterial
        {
            Code = code,
            Name = name,
            Description = description,
            Category = category,
            Unit = unit,
            CurrentStock = 0,
            MinimumStock = minimumStock,
            MaximumStock = maximumStock,
            AverageCost = 0,
            LastPurchaseCost = 0,
            IsRecycled = isRecycled,
            IsReusable = isReusable,
            RequiresPurchase = requiresPurchase,
            StorageLocation = storageLocation,
            PreferredSupplierId = null,
            PreferredSupplierName = null,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SystemSeed"
        };
    }
}