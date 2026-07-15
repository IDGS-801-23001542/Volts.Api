using MongoDB.Driver;
using Volts.Api.Models;
using Volts.Api.Models.Common;
using Volts.Api.Models.Enums;

namespace Volts.Api.Services;

public class SeedService
{
    private const string SeedUser = "SystemSeed";
    private readonly MongoDbService _db;

    public SeedService(MongoDbService db)
    {
        _db = db;
    }

    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedEmployeeAsync();
        await SeedUnitsAsync();
        await SeedCategoriesAsync();
        await SeedProductsAsync();
        await SeedSuppliersAsync();
        await SeedRawMaterialsAsync();
        await SeedInitialPurchasesAsync();
        await EnsureIndexesAsync();
    }

    private async Task SeedRolesAsync()
    {
        var roles = new[]
        {
            new Role
            {
                Name = "Admin",
                Description = "Administrador general del sistema",
                Permissions = new List<string> { "*" },
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            },
            new Role
            {
                Name = "Employee",
                Description = "Empleado operativo del backoffice",
                Permissions = new List<string>
                {
                    "dashboard.read",
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
                    "waste.create"
                },
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            },
            new Role
            {
                Name = "Client",
                Description = "Cliente del portal VOLTS",
                Permissions = new List<string>
                {
                    "profile.read",
                    "profile.update",
                    "quotes.create",
                    "quotes.read-own",
                    "orders.read-own",
                    "licenses.read-own"
                },
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            }
        };

        foreach (var role in roles)
        {
            var existing = await _db.Roles
                .Find(x => x.Name == role.Name && !x.IsDeleted)
                .FirstOrDefaultAsync();

            if (existing == null)
            {
                await _db.Roles.InsertOneAsync(role);
                continue;
            }

            existing.Description = role.Description;
            existing.Permissions = role.Permissions;
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = SeedUser;

            await _db.Roles.ReplaceOneAsync(
                x => x.Id == existing.Id,
                existing
            );
        }
    }

    private async Task SeedEmployeeAsync()
    {
        const string email = "voltsempleado@gmail.com";
        const string password = "Empleado123!";

        var employeeRole = await _db.Roles
            .Find(x => x.Name == "Employee" && x.IsActive && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (employeeRole == null)
        {
            throw new InvalidOperationException("No existe el rol Employee.");
        }

        var existing = await _db.Users
            .Find(x => x.Email == email && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (existing == null)
        {
            await _db.Users.InsertOneAsync(new User
            {
                Name = new PersonName
                {
                    FirstNames = "Empleado",
                    PaternalLastName = "VOLTS",
                    MaternalLastName = null
                },
                LegacyFullName = "Empleado VOLTS",
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                RoleId = employeeRole.Id,
                RoleName = employeeRole.Name,
                UserType = UserType.Employee,
                ProfileId = null,
                IsActive = true,
                IsEmailConfirmed = true,
                TwoFactorEnabled = false,
                FailedLoginAttempts = 0,
                LockoutEnd = null,
                LastLoginAt = null,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            });
            return;
        }

        existing.Name = new PersonName
        {
            FirstNames = "Empleado",
            PaternalLastName = "VOLTS",
            MaternalLastName = null
        };
        existing.LegacyFullName = "Empleado VOLTS";
        existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        existing.RoleId = employeeRole.Id;
        existing.RoleName = employeeRole.Name;
        existing.UserType = UserType.Employee;
        existing.ProfileId = null;
        existing.IsActive = true;
        existing.IsEmailConfirmed = true;
        existing.TwoFactorEnabled = false;
        existing.FailedLoginAttempts = 0;
        existing.LockoutEnd = null;
        existing.IsDeleted = false;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = SeedUser;

        await _db.Users.ReplaceOneAsync(
            x => x.Id == existing.Id,
            existing
        );
    }

    private async Task SeedUnitsAsync()
    {
        var units = new[]
        {
            BuildUnit("Piece", "Pieza", "Piezas", "pza", false, 0),
            BuildUnit("Unit", "Unidad", "Unidades", "ud", false, 0),
            BuildUnit("Kit", "Kit", "Kits", "kit", false, 0),
            BuildUnit("Sheet", "Lámina", "Láminas", "lám", false, 0),
            BuildUnit("Kilogram", "Kilogramo", "Kilogramos", "kg", true, 4),
            BuildUnit("Gram", "Gramo", "Gramos", "g", true, 4),
            BuildUnit("Liter", "Litro", "Litros", "L", true, 4),
            BuildUnit("Milliliter", "Mililitro", "Mililitros", "ml", true, 4),
            BuildUnit("Meter", "Metro", "Metros", "m", true, 4),
            BuildUnit("SquareMeter", "Metro cuadrado", "Metros cuadrados", "m²", true, 4)
        };

        foreach (var unit in units)
        {
            if (!await _db.UnitsOfMeasure
                .Find(x => x.Code == unit.Code && !x.IsDeleted)
                .AnyAsync())
            {
                await _db.UnitsOfMeasure.InsertOneAsync(unit);
            }
        }
    }

    private async Task SeedCategoriesAsync()
    {
        var categories = new[]
        {
            BuildCategory("Robot educativo", "Perros robot educativos VOLTS."),
            BuildCategory("Accesorios", "Accesorios y complementos para VOLTS."),
            BuildCategory("Refacciones", "Refacciones y componentes de reemplazo.")
        };

        foreach (var category in categories)
        {
            if (!await _db.Categories
                .Find(x => x.Name == category.Name && !x.IsDeleted)
                .AnyAsync())
            {
                await _db.Categories.InsertOneAsync(category);
            }
        }
    }

    private async Task SeedProductsAsync()
    {
        var category = await _db.Categories
            .Find(x => x.Name == "Robot educativo" && !x.IsDeleted)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                "No existe la categoría Robot educativo."
            );

        var products = new[]
        {
            BuildProduct("VOLTS Husky", "volts-husky", "Perro robot educativo VOLTS modelo Husky.", "Husky", category, "Available", true, true),
            BuildProduct("VOLTS Rottweiler", "volts-rottweiler", "Perro robot educativo VOLTS modelo Rottweiler.", "Rottweiler", category, "ComingSoon", false, true),
            BuildProduct("VOLTS Caramelo", "volts-caramelo", "Perro robot educativo VOLTS modelo Caramelo.", "Caramelo", category, "ComingSoon", false, true)
        };

        foreach (var product in products)
        {
            if (!await _db.Products
                .Find(x => x.Slug == product.Slug && !x.IsDeleted)
                .AnyAsync())
            {
                await _db.Products.InsertOneAsync(product);
            }
        }
    }

    private async Task SeedSuppliersAsync()
    {
        var suppliers = new[]
        {
            BuildSupplier(
                "SUP-ELE-001",
                "Electrónica del Bajío",
                "Electrónica del Bajío S.A. de C.V.",
                "EBA260101AA1",
                "Carlos Ramírez",
                "ventas@electronicabajio.mx",
                "4771234567",
                "Electronics",
                new[] { "Electronics", "Mechanical", "Soldering" },
                "Boulevard Adolfo López Mateos",
                "1200",
                "Centro",
                "37000",
                "Local de componentes electrónicos."
            ),
            BuildSupplier(
                "SUP-REC-001",
                "Reciclados del Bajío",
                "Reciclados del Bajío S.A. de C.V.",
                "RBA260101BB2",
                "Mariana Torres",
                "contacto@recicladosbajio.mx",
                "4777654321",
                "RecycledMaterials",
                new[] { "Cardboard", "Packaging", "Adhesives" },
                "Avenida Industria",
                "450",
                "Industrial",
                "37490",
                "Bodega de cartón y adhesivos."
            ),
            BuildSupplier(
                "SUP-TEX-001",
                "Textiles León",
                "Textiles León S.A. de C.V.",
                "TLE260101CC3",
                "Andrea Mendoza",
                "ventas@textilesleon.mx",
                "4772223344",
                "Textiles",
                new[] { "Textiles" },
                "Calle Piel",
                "215",
                "Obregón",
                "37320",
                "Proveedor de telas para recubrimiento."
            ),
            BuildSupplier(
                "SUP-MAN-001",
                "Manualidades y Maderas León",
                "Manualidades y Maderas León S.A. de C.V.",
                "MML260101DD4",
                "Sofía Hernández",
                "pedidos@manualidadesleon.mx",
                "4774455667",
                "Crafts",
                new[] { "Wood", "Paint", "Adhesives", "Consumables" },
                "Avenida Artesanos",
                "88",
                "San Juan de Dios",
                "37004",
                "Proveedor de madera, pinturas y cinta."
            )
        };

        foreach (var supplier in suppliers)
        {
            if (!await _db.Suppliers
                .Find(x => x.Code == supplier.Code && !x.IsDeleted)
                .AnyAsync())
            {
                await _db.Suppliers.InsertOneAsync(supplier);
            }
        }
    }

    private async Task SeedRawMaterialsAsync()
    {
        var piece = await GetUnitAsync("Piece");
        var kilogram = await GetUnitAsync("Kilogram");
        var meter = await GetUnitAsync("Meter");
        var milliliter = await GetUnitAsync("Milliliter");
        var squareMeter = await GetUnitAsync("SquareMeter");

        var electronics = await GetSupplierAsync("SUP-ELE-001");
        var recycled = await GetSupplierAsync("SUP-REC-001");
        var textiles = await GetSupplierAsync("SUP-TEX-001");
        var crafts = await GetSupplierAsync("SUP-MAN-001");

        var materials = new[]
        {
            BuildMaterial("RAW-ELE-001", "ESP32 DevKit", "Tarjeta principal de control del robot.", "Electronics", piece, 10, 100, "A-01", electronics),
            BuildMaterial("RAW-MEC-001", "Servomotor SG90", "Servomotor para las cuatro patas.", "Mechanical", piece, 40, 500, "A-02", electronics),
            BuildMaterial("RAW-ELE-002", "LED RGB", "Indicador visual del estado.", "Electronics", piece, 20, 200, "A-03", electronics),
            BuildMaterial("RAW-ELE-003", "Botón pulsador", "Botón físico de interacción.", "Electronics", piece, 20, 200, "A-04", electronics),
            BuildMaterial("RAW-ELE-004", "Switch", "Interruptor general de encendido.", "Electronics", piece, 20, 200, "A-05", electronics),
            BuildMaterial("RAW-ELE-005", "Portapilas de 4 AA", "Portapilas del robot.", "Electronics", piece, 10, 100, "A-06", electronics),
            BuildMaterial("RAW-ELE-006", "Resistencia de 220 Ω", "Resistencia para el LED RGB.", "Electronics", piece, 100, 2000, "A-07", electronics),
            BuildMaterial("RAW-ELE-008", "Placa perforada para soldar", "Placa de ensamble y soldadura.", "Soldering", piece, 10, 100, "A-08", electronics),
            BuildMaterial("RAW-ELE-009", "Cable Dupont", "Cable Dupont individual para conexiones internas.", "Electronics", piece, 120, 2000, "A-09", electronics),
            BuildMaterial("RAW-MEC-002", "Wire cover flexible", "Recubrimiento flexible para el movimiento de las patas.", "Mechanical", meter, 2m, 50m, "A-10", electronics),
            BuildMaterial("RAW-CAR-001", "Cartón reciclado", "Cartón para la estructura del cuerpo.", "Cardboard", kilogram, 5m, 50m, "B-01", recycled, true, true),
            BuildMaterial("RAW-ADH-001", "Adhesivo de silicón", "Adhesivo para ensamble.", "Adhesives", milliliter, 500m, 10000m, "B-02", recycled),
            BuildMaterial("RAW-TEX-001", "Tela gris", "Tela gris para el modelo Husky.", "Textiles", squareMeter, 2m, 30m, "C-01", textiles),
            BuildMaterial("RAW-TEX-002", "Tela blanca", "Tela blanca para detalles.", "Textiles", squareMeter, 2m, 30m, "C-02", textiles),
            BuildMaterial("RAW-TEX-003", "Tela negra", "Tela negra para recubrimiento y detalles.", "Textiles", squareMeter, 2m, 30m, "C-03", textiles),
            BuildMaterial("RAW-TEX-004", "Tela café", "Tela café para Caramelo y Rottweiler.", "Textiles", squareMeter, 2m, 30m, "C-04", textiles),
            BuildMaterial("RAW-MAD-001", "Palito de madera tipo paleta", "Palito para el mecanismo de las patas.", "Wood", piece, 40, 1000, "D-01", crafts, false, true),
            BuildMaterial("RAW-MAD-002", "Varilla de madera", "Varilla para el mecanismo de las patas.", "Wood", piece, 40, 1000, "D-02", crafts, false, true),
            BuildMaterial("RAW-PNT-001", "Pintura acrílica negra", "Pintura para ojos, nariz y detalles.", "Paint", milliliter, 100m, 2000m, "D-03", crafts),
            BuildMaterial("RAW-PNT-002", "Pintura acrílica blanca", "Pintura para ojos y detalles.", "Paint", milliliter, 100m, 2000m, "D-04", crafts),
            BuildMaterial("RAW-PNT-003", "Pintura acrílica roja", "Pintura para lengua y detalles.", "Paint", milliliter, 50m, 1000m, "D-05", crafts),
            BuildMaterial("RAW-ADH-002", "Cinta doble cara", "Cinta para fijación de tela y acabados.", "Adhesives", meter, 10m, 200m, "D-06", crafts)
        };

        foreach (var material in materials)
        {
            if (!await _db.RawMaterials
                .Find(x => x.Code == material.Code && !x.IsDeleted)
                .AnyAsync())
            {
                await _db.RawMaterials.InsertOneAsync(material);
            }
        }
    }

    private async Task SeedInitialPurchasesAsync()
    {
        await CreatePurchaseIfMissingAsync(
            "PUR-SEED-ELE-001",
            "SUP-ELE-001",
            new Dictionary<string, (decimal Quantity, decimal UnitCost)>
            {
                ["RAW-ELE-001"] = (30m, 180m),
                ["RAW-MEC-001"] = (120m, 45m),
                ["RAW-ELE-002"] = (50m, 8m),
                ["RAW-ELE-003"] = (50m, 6m),
                ["RAW-ELE-004"] = (50m, 10m),
                ["RAW-ELE-005"] = (30m, 35m),
                ["RAW-ELE-006"] = (400m, 0.50m),
                ["RAW-ELE-008"] = (30m, 22m),
                ["RAW-ELE-009"] = (360m, 1.50m),
                ["RAW-MEC-002"] = (20m, 15m)
            }
        );

        await CreatePurchaseIfMissingAsync(
            "PUR-SEED-REC-001",
            "SUP-REC-001",
            new Dictionary<string, (decimal Quantity, decimal UnitCost)>
            {
                ["RAW-CAR-001"] = (20m, 18.50m),
                ["RAW-ADH-001"] = (5000m, 0.08m)
            }
        );

        await CreatePurchaseIfMissingAsync(
            "PUR-SEED-TEX-001",
            "SUP-TEX-001",
            new Dictionary<string, (decimal Quantity, decimal UnitCost)>
            {
                ["RAW-TEX-001"] = (10m, 90m),
                ["RAW-TEX-002"] = (10m, 88m),
                ["RAW-TEX-003"] = (10m, 92m),
                ["RAW-TEX-004"] = (10m, 89m)
            }
        );

        await CreatePurchaseIfMissingAsync(
            "PUR-SEED-MAN-001",
            "SUP-MAN-001",
            new Dictionary<string, (decimal Quantity, decimal UnitCost)>
            {
                ["RAW-MAD-001"] = (200m, 0.80m),
                ["RAW-MAD-002"] = (200m, 1.20m),
                ["RAW-PNT-001"] = (500m, 0.12m),
                ["RAW-PNT-002"] = (500m, 0.12m),
                ["RAW-PNT-003"] = (250m, 0.14m),
                ["RAW-ADH-002"] = (50m, 8m)
            }
        );
    }

    private async Task CreatePurchaseIfMissingAsync(
        string folio,
        string supplierCode,
        Dictionary<string, (decimal Quantity, decimal UnitCost)> items)
    {
        if (await _db.Purchases
            .Find(x => x.Folio == folio && !x.IsDeleted)
            .AnyAsync())
        {
            return;
        }

        await CreatePurchaseAsync(
            folio,
            await GetSupplierAsync(supplierCode),
            items
        );
    }

    private async Task CreatePurchaseAsync(
        string folio,
        Supplier supplier,
        Dictionary<string, (decimal Quantity, decimal UnitCost)> items)
    {
        var codes = items.Keys.ToList();
        var materials = await _db.RawMaterials
            .Find(x => codes.Contains(x.Code) && !x.IsDeleted && x.IsActive)
            .ToListAsync();

        var missing = codes.Except(materials.Select(x => x.Code)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "No se encontraron las materias primas: " +
                string.Join(", ", missing)
            );
        }

        var purchase = new Purchase
        {
            Folio = folio,
            InvoiceNumber = folio,
            SupplierId = supplier.Id,
            SupplierCode = supplier.Code,
            SupplierName = supplier.Name,
            PurchaseDate = DateTime.UtcNow,
            Status = "Completed",
            Notes = "Compra inicial generada por el seed definitivo.",
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = SeedUser
        };

        foreach (var material in materials)
        {
            var item = items[material.Code];
            ValidateSeedQuantity(material, item.Quantity);

            if (item.UnitCost <= 0)
            {
                throw new InvalidOperationException(
                    $"El costo unitario de {material.Name} debe ser mayor a cero."
                );
            }

            var previousStock = material.CurrentStock;
            var previousAverage = material.AverageCost;
            var newStock = previousStock + item.Quantity;

            if (material.MaximumStock > 0 && newStock > material.MaximumStock)
            {
                throw new InvalidOperationException(
                    $"La compra {folio} supera el stock máximo de {material.Name}."
                );
            }

            var newAverage = newStock == 0
                ? item.UnitCost
                : (
                    previousStock * previousAverage +
                    item.Quantity * item.UnitCost
                ) / newStock;

            newAverage = InventoryRoundingService.RoundUnitCost(newAverage);
            var subtotal = InventoryRoundingService.RoundMoney(
                item.Quantity * item.UnitCost
            );

            purchase.Details.Add(new PurchaseDetail
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
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
                Subtotal = subtotal,
                PreviousStock = previousStock,
                NewStock = newStock,
                PreviousAverageCost = previousAverage,
                NewAverageCost = newAverage
            });

            purchase.Subtotal += subtotal;
        }

        purchase.Subtotal = InventoryRoundingService.RoundMoney(purchase.Subtotal);
        purchase.Total = InventoryRoundingService.RoundMoney(
            purchase.Subtotal + purchase.Tax + purchase.ShippingCost
        );

        using var session = await _db.StartSessionAsync();
        session.StartTransaction();

        try
        {
            await _db.Purchases.InsertOneAsync(session, purchase);

            foreach (var detail in purchase.Details)
            {
                var material = materials.First(x => x.Id == detail.RawMaterialId);
                material.CurrentStock = detail.NewStock;
                material.AverageCost = detail.NewAverageCost;
                material.LastPurchaseCost = detail.UnitCost;
                material.PreferredSupplierId = supplier.Id;
                material.PreferredSupplierName = supplier.Name;
                material.UpdatedAt = DateTime.UtcNow;
                material.UpdatedBy = SeedUser;

                await _db.RawMaterials.ReplaceOneAsync(
                    session,
                    x => x.Id == material.Id,
                    material
                );

                await _db.RawMaterialMovements.InsertOneAsync(
                    session,
                    new RawMaterialMovement
                    {
                        RawMaterialId = material.Id,
                        RawMaterialCode = material.Code,
                        RawMaterialName = material.Name,
                        MovementType = "PurchaseEntry",
                        Quantity = detail.Quantity,
                        PreviousStock = detail.PreviousStock,
                        NewStock = detail.NewStock,
                        UnitOfMeasureId = material.UnitOfMeasureId,
                        UnitCode = material.UnitCode,
                        UnitName = material.UnitName,
                        UnitSymbol = material.UnitSymbol,
                        UnitAllowsDecimals = material.UnitAllowsDecimals,
                        UnitDecimalPlaces = material.UnitDecimalPlaces,
                        Unit = material.UnitSymbol,
                        Reason = $"Compra inicial {purchase.Folio}",
                        ReferenceType = "Purchase",
                        ReferenceId = purchase.Id,
                        UnitCost = detail.UnitCost,
                        TotalCost = detail.Subtotal,
                        MovementDate = purchase.PurchaseDate,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = SeedUser
                    }
                );
            }

            await session.CommitTransactionAsync();
        }
        catch
        {
            await session.AbortTransactionAsync();
            throw;
        }
    }

    private async Task EnsureIndexesAsync()
    {
        await CreateUniqueIndexAsync(_db.UnitsOfMeasure, Builders<UnitOfMeasure>.IndexKeys.Ascending(x => x.Code), "UX_UnitsOfMeasure_Code");
        await CreateUniqueIndexAsync(_db.Categories, Builders<Category>.IndexKeys.Ascending(x => x.Name), "UX_Categories_Name");
        await CreateUniqueIndexAsync(_db.Products, Builders<Product>.IndexKeys.Ascending(x => x.Slug), "UX_Products_Slug");
        await CreateUniqueIndexAsync(_db.Suppliers, Builders<Supplier>.IndexKeys.Ascending(x => x.Code), "UX_Suppliers_Code");
        await CreateUniqueIndexAsync(_db.RawMaterials, Builders<RawMaterial>.IndexKeys.Ascending(x => x.Code), "UX_RawMaterials_Code");
        await CreateUniqueIndexAsync(_db.Purchases, Builders<Purchase>.IndexKeys.Ascending(x => x.Folio), "UX_Purchases_Folio");
    }

    private static async Task CreateUniqueIndexAsync<T>(
        IMongoCollection<T> collection,
        IndexKeysDefinition<T> keys,
        string name)
    {
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<T>(
                keys,
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = name
                }
            )
        );
    }

    private static UnitOfMeasure BuildUnit(
        string code,
        string singular,
        string plural,
        string symbol,
        bool allowsDecimals,
        int decimalPlaces)
    {
        return new UnitOfMeasure
        {
            Code = code,
            SingularName = singular,
            PluralName = plural,
            Symbol = symbol,
            AllowsDecimals = allowsDecimals,
            DecimalPlaces = decimalPlaces,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = SeedUser
        };
    }

    private static Category BuildCategory(string name, string description)
    {
        return new Category
        {
            Name = name,
            Description = description,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = SeedUser
        };
    }

    private static Product BuildProduct(
        string name,
        string slug,
        string description,
        string breed,
        Category category,
        string commercialStatus,
        bool canBePurchased,
        bool canBeProduced)
    {
        return new Product
        {
            Name = name,
            Slug = slug,
            Description = description,
            Price = 549m,
            CategoryId = category.Id,
            CategoryName = category.Name,
            Category = category.Name,
            Species = "Perro",
            Breed = breed,
            CommercialStatus = commercialStatus,
            CanBePurchased = canBePurchased,
            CanBeProduced = canBeProduced,
            ImageUrl = null,
            PhysicalStock = 0,
            ReservedStock = 0,
            MinimumFinishedStock = 5,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = SeedUser
        };
    }

    private static Supplier BuildSupplier(
        string code,
        string name,
        string legalName,
        string taxId,
        string contactName,
        string email,
        string phone,
        string supplierType,
        IEnumerable<string> categories,
        string street,
        string exteriorNumber,
        string neighborhood,
        string postalCode,
        string references)
    {
        return new Supplier
        {
            Code = code,
            Name = name,
            LegalName = legalName,
            TaxId = taxId,
            ContactName = contactName,
            Email = email,
            Phone = phone,
            Address = new Address
            {
                Street = street,
                ExteriorNumber = exteriorNumber,
                InteriorNumber = null,
                Neighborhood = neighborhood,
                PostalCode = postalCode,
                City = "León",
                State = "Guanajuato",
                Country = "México",
                References = references
            },
            SupplierType = supplierType,
            MaterialCategories = categories.ToList(),
            LeadTimeDays = 3,
            PaymentTerms = "Contado",
            Notes = "Proveedor inicial generado por el seed.",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = SeedUser
        };
    }

    private static RawMaterial BuildMaterial(
        string code,
        string name,
        string description,
        string category,
        UnitOfMeasure unit,
        decimal minimumStock,
        decimal maximumStock,
        string location,
        Supplier supplier,
        bool isRecycled = false,
        bool isReusable = false)
    {
        return new RawMaterial
        {
            Code = code,
            Name = name,
            Description = description,
            Category = category,
            UnitOfMeasureId = unit.Id,
            UnitCode = unit.Code,
            UnitName = unit.SingularName,
            UnitSymbol = unit.Symbol,
            UnitAllowsDecimals = unit.AllowsDecimals,
            UnitDecimalPlaces = unit.DecimalPlaces,
            Unit = unit.Symbol,
            CurrentStock = 0,
            MinimumStock = minimumStock,
            MaximumStock = maximumStock,
            AverageCost = 0,
            LastPurchaseCost = 0,
            IsRecycled = isRecycled,
            IsReusable = isReusable,
            RequiresPurchase = true,
            StorageLocation = location,
            PreferredSupplierId = supplier.Id,
            PreferredSupplierName = supplier.Name,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = SeedUser
        };
    }

    private static void ValidateSeedQuantity(
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

    private async Task<UnitOfMeasure> GetUnitAsync(string code)
    {
        return await _db.UnitsOfMeasure
            .Find(x => x.Code == code && !x.IsDeleted)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                $"No existe la unidad {code}."
            );
    }

    private async Task<Supplier> GetSupplierAsync(string code)
    {
        return await _db.Suppliers
            .Find(x => x.Code == code && !x.IsDeleted)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                $"No existe el proveedor {code}."
            );
    }
}