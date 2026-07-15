using MongoDB.Driver;
using Volts.Api.Models;
using Volts.Api.Models.Common;
using Volts.Api.Models.Enums;

namespace Volts.Api.Services;

public class CommercialDemoSeedService
{
    private const string SeedUser = "CommercialDemoSeed";
    private readonly MongoDbService _db;

    public CommercialDemoSeedService(MongoDbService db)
    {
        _db = db;
    }

    public async Task SeedAsync()
    {
        await SeedPortalAccountsAsync();
        await SeedCommercialHistoryAsync();
        await SeedCommentsAsync();
        await SeedEtlHistoryAsync();
        await SeedAuditHistoryAsync();
        await SeedNotificationsAsync();
    }

    private async Task SeedPortalAccountsAsync()
    {
        var clientRole = await GetRoleAsync("Client");
        var institutionRole = await GetRoleAsync("Institution");

        var customers = await _db.Customers
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        foreach (var customer in customers)
        {
            var password = customer.Email switch
            {
                "pancracio.lomito@volts.test" =>
                    "VoltsPancracio2026!",
                "teofilo.croquetas@volts.test" =>
                    "VoltsTeofilo2026!",
                "firulais.roble@volts.test" =>
                    "VoltsFirulais2026!",
                _ => "VoltsCliente2026!"
            };

            await UpsertUserAsync(
                customer.Email,
                customer.Name,
                password,
                clientRole,
                UserType.Customer,
                customer.Id
            );
        }

        var institutions = await _db.Institutions
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        foreach (var institution in institutions)
        {
            var password = institution.Name switch
            {
                "Jardín de Niños Patitas del Saber" =>
                    "VoltsPatitas2026!",
                "Kinder Pequeños Inventores del Bajío" =>
                    "VoltsInventores2026!",
                "Instituto Preescolar Firulais Einstein" =>
                    "VoltsEinstein2026!",
                _ => "VoltsInstitucion2026!"
            };

            await UpsertUserAsync(
                institution.Responsible.Email,
                institution.Responsible.Name,
                password,
                institutionRole,
                UserType.Institution,
                institution.Id
            );
        }
    }

    private async Task SeedCommercialHistoryAsync()
    {
        if (await _db.Quotes
            .Find(item =>
                item.CreatedBy == SeedUser &&
                !item.IsDeleted)
            .AnyAsync())
        {
            return;
        }

        var customers = await _db.Customers
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var institutions = await _db.Institutions
            .Find(item => !item.IsDeleted)
            .ToListAsync();

        var packages = await _db.CommercialPackages
            .Find(item =>
                item.IsActive &&
                !item.IsDeleted)
            .ToListAsync();

        var plans = await _db.CommercialPlans
            .Find(item =>
                item.IsActive &&
                !item.IsDeleted)
            .ToListAsync();

        if (customers.Count < 3 ||
            institutions.Count < 3 ||
            packages.Count < 3 ||
            plans.Count < 3)
        {
            return;
        }

        var packageByCode = packages.ToDictionary(
            item => item.Code,
            StringComparer.OrdinalIgnoreCase
        );

        var planById = plans.ToDictionary(
            item => item.Id
        );

        var now = DateTime.UtcNow;

        var scenarios = new[]
        {
            new Scenario(
                "Customer",
                customers[0],
                null,
                "PKG-INDIVIDUAL-1",
                1,
                "Sold",
                now.AddMonths(-7).AddDays(4)
            ),
            new Scenario(
                "Customer",
                customers[1],
                null,
                "PKG-INDIVIDUAL-1",
                1,
                "Sold",
                now.AddMonths(-6).AddDays(7)
            ),
            new Scenario(
                "Institution",
                null,
                institutions[0],
                "PKG-AULA-5",
                1,
                "Sold",
                now.AddMonths(-5).AddDays(10)
            ),
            new Scenario(
                "Customer",
                customers[2],
                null,
                "PKG-INDIVIDUAL-1",
                1,
                "Sold",
                now.AddMonths(-4).AddDays(12)
            ),
            new Scenario(
                "Institution",
                null,
                institutions[1],
                "PKG-AULA-5",
                1,
                "Sold",
                now.AddMonths(-3).AddDays(9)
            ),
            new Scenario(
                "Institution",
                null,
                institutions[2],
                "PKG-LAB-10",
                1,
                "Sold",
                now.AddMonths(-2).AddDays(6)
            ),
            new Scenario(
                "Customer",
                customers[0],
                null,
                "PKG-INDIVIDUAL-1",
                1,
                "ReadyForSale",
                now.AddDays(-12)
            ),
            new Scenario(
                "Institution",
                null,
                institutions[0],
                "PKG-AULA-5",
                1,
                "AwaitingProduction",
                now.AddDays(-8)
            ),
            new Scenario(
                "Customer",
                customers[1],
                null,
                "PKG-INDIVIDUAL-1",
                1,
                "PendingConfirmation",
                now.AddDays(-4)
            )
        };

        var quotes = new List<Quote>();
        var orders = new List<Order>();
        var sales = new List<Sale>();
        var licenses = new List<License>();

        foreach (var scenario in scenarios)
        {
            var package = packageByCode[scenario.PackageCode];
            var plan = planById[package.CommercialPlanId];

            var quoteStatus = scenario.OrderStatus == "PendingConfirmation"
                ? "Approved"
                : "Converted";

            var quote = BuildQuote(
                scenario,
                plan,
                package,
                quoteStatus
            );

            var order = BuildOrder(
                scenario,
                quote
            );

            if (quoteStatus == "Converted")
            {
                quote.ConvertedOrderId = order.Id;
            }

            quotes.Add(quote);
            orders.Add(order);

            if (scenario.OrderStatus != "Sold")
            {
                continue;
            }

            var sale = BuildSale(
                order,
                scenario.CreatedAt.AddDays(2)
            );

            var generatedLicenses = BuildLicenses(
                sale,
                order,
                plan
            );

            sale.LicenseIds = generatedLicenses
                .Select(item => item.Id)
                .ToList();

            sales.Add(sale);
            licenses.AddRange(generatedLicenses);
        }

        await _db.Quotes.InsertManyAsync(quotes);
        await _db.Orders.InsertManyAsync(orders);
        await _db.Sales.InsertManyAsync(sales);
        await _db.Licenses.InsertManyAsync(licenses);

        var productReservations = orders
            .Where(item => item.Status == "ReadyForSale")
            .SelectMany(item => item.Details)
            .GroupBy(item => item.ProductId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item =>
                    item.ReservedQuantity)
            );

        foreach (var pair in productReservations)
        {
            var product = await _db.Products
                .Find(item =>
                    item.Id == pair.Key &&
                    !item.IsDeleted)
                .FirstOrDefaultAsync();

            if (product == null)
                continue;

            product.PhysicalStock = Math.Max(
                product.PhysicalStock,
                pair.Value + 12
            );

            product.ReservedStock = pair.Value;
            product.UpdatedAt = now;
            product.UpdatedBy = SeedUser;

            await _db.Products.ReplaceOneAsync(
                item => item.Id == product.Id,
                product
            );
        }
    }

    private async Task SeedCommentsAsync()
    {
        if (await _db.Comments
            .Find(item =>
                item.CreatedBy == SeedUser &&
                !item.IsDeleted)
            .AnyAsync())
        {
            return;
        }

        var comments = new[]
        {
            BuildComment(
                "Pancracio Lomito Pérez",
                "pancracio.lomito@volts.test",
                "VOLTS hizo que aprender programación en casa fuera mucho más divertido.",
                5,
                true,
                DateTime.UtcNow.AddDays(-40)
            ),
            BuildComment(
                "Teófilo Croquetas Ramírez",
                "teofilo.croquetas@volts.test",
                "La compra fue clara y el robot llegó listo para comenzar.",
                5,
                true,
                DateTime.UtcNow.AddDays(-34)
            ),
            BuildComment(
                "Lupita Galletitas Mendoza",
                "lupita.galletitas@patitas.test",
                "Los alumnos se involucraron rápidamente con las actividades del perro robot.",
                5,
                true,
                DateTime.UtcNow.AddDays(-28)
            ),
            BuildComment(
                "Tomás Tornillito Pérez",
                "tomas.tornillito@inventores.test",
                "El paquete educativo facilita organizar prácticas de electrónica y programación.",
                4,
                true,
                DateTime.UtcNow.AddDays(-20)
            ),
            BuildComment(
                "Roberta Croquetina López",
                "roberta.croquetina@firulais.test",
                "Estamos evaluando ampliar el laboratorio con más unidades VOLTS.",
                4,
                false,
                DateTime.UtcNow.AddDays(-6)
            ),
            BuildComment(
                "Firulais Antonio Del Roble Sánchez",
                "firulais.roble@volts.test",
                "Me gustaría contar con más guías para proyectos avanzados.",
                4,
                false,
                DateTime.UtcNow.AddDays(-3)
            )
        };

        await _db.Comments.InsertManyAsync(comments);
    }

    private async Task SeedEtlHistoryAsync()
    {
        if (await _db.EtlLogs
            .Find(item =>
                item.CreatedBy == SeedUser &&
                !item.IsDeleted)
            .AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;

        await _db.EtlLogs.InsertManyAsync(
            new[]
            {
                BuildEtl(
                    "Normalización comercial",
                    94,
                    94,
                    0,
                    "Completed",
                    now.AddDays(-7)
                ),
                BuildEtl(
                    "Calidad de inventario",
                    67,
                    65,
                    2,
                    "CompletedWithWarnings",
                    now.AddDays(-4)
                ),
                BuildEtl(
                    "Snapshot de analítica",
                    131,
                    131,
                    0,
                    "Completed",
                    now.AddDays(-1)
                )
            }
        );
    }

    private async Task SeedAuditHistoryAsync()
    {
        if (await _db.AuditLogs
            .Find(item =>
                item.CreatedBy == SeedUser &&
                !item.IsDeleted)
            .AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;

        var logs = new[]
        {
            BuildAudit(
                "Administración",
                "Crear",
                "Users",
                "Se generaron cuentas de portal para clientes e instituciones.",
                now.AddDays(-10)
            ),
            BuildAudit(
                "Comercial",
                "Convertir",
                "Quotes",
                "Se convirtió una cotización aprobada en pedido.",
                now.AddDays(-9)
            ),
            BuildAudit(
                "Comercial",
                "Confirmar",
                "Orders",
                "Se confirmó un pedido y se reservaron existencias.",
                now.AddDays(-8)
            ),
            BuildAudit(
                "Producción e inventario",
                "Crear",
                "Production",
                "Se generó producción por faltante de un pedido.",
                now.AddDays(-7)
            ),
            BuildAudit(
                "Comercial",
                "Crear",
                "Sales",
                "Se confirmó una venta y se generaron licencias.",
                now.AddDays(-6)
            ),
            BuildAudit(
                "Atención y contenido",
                "Actualizar",
                "Comments",
                "Se aprobó un comentario para el sitio público.",
                now.AddDays(-5)
            ),
            BuildAudit(
                "Administración",
                "Ejecutar",
                "EtlLogs",
                "Se ejecutó el snapshot empresarial para analítica.",
                now.AddDays(-1)
            )
        };

        await _db.AuditLogs.InsertManyAsync(logs);
    }

    private async Task SeedNotificationsAsync()
    {
        if (await _db.Notifications.Find(item => item.CreatedBy == SeedUser && !item.IsDeleted).AnyAsync())
            return;

        var internalUsers = await _db.Users.Find(item =>
            !item.IsDeleted && item.IsActive && (item.RoleName == "Admin" || item.RoleName == "Employee"))
            .ToListAsync();

        var notifications = new List<Notification>();
        foreach (var user in internalUsers)
        {
            notifications.AddRange(new[]
            {
                BuildNotification(user, "Pedido esperando producción", "Un pedido institucional requiere producir unidades faltantes.", "Production", "High", "Comercial", "/backoffice/pedidos"),
                BuildNotification(user, "Materia prima bajo mínimo", "Revisa los materiales marcados con stock bajo antes de iniciar producción.", "Inventory", "High", "Producción e inventario", "/backoffice/materia-prima"),
                BuildNotification(user, "Comentarios pendientes", "Hay comentarios del sitio público esperando aprobación.", "Content", "Normal", "Atención y contenido", "/backoffice/comentarios")
            });
        }
        if (notifications.Count > 0) await _db.Notifications.InsertManyAsync(notifications);
    }

    private static Notification BuildNotification(User user, string title, string message, string type, string priority, string module, string route)
    {
        return new Notification
        {
            UserId = user.Id,
            UserName = user.FullName,
            Title = title,
            Message = message,
            Type = type,
            Priority = priority,
            Module = module,
            Route = route,
            IsRead = false,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            CreatedBy = SeedUser
        };
    }

    private async Task<Role> GetRoleAsync(string name)
    {
        return await _db.Roles
            .Find(item =>
                item.Name == name &&
                item.IsActive &&
                !item.IsDeleted)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                $"No existe el rol {name}."
            );
    }

    private async Task UpsertUserAsync(
        string email,
        PersonName name,
        string password,
        Role role,
        UserType userType,
        string profileId)
    {
        var normalizedEmail = email
            .Trim()
            .ToLowerInvariant();

        var user = await _db.Users
            .Find(item =>
                item.Email == normalizedEmail &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            user = new User
            {
                Name = name,
                LegacyFullName = name.FullName,
                Email = normalizedEmail,
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        password
                    ),
                RoleId = role.Id,
                RoleName = role.Name,
                UserType = userType,
                ProfileId = profileId,
                IsActive = true,
                IsEmailConfirmed = true,
                MustChangePassword = true,
                TwoFactorEnabled = false,
                FailedLoginAttempts = 0,
                LockoutEnd = null,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            };

            await _db.Users.InsertOneAsync(user);
            return;
        }

        user.Name = name;
        user.LegacyFullName = name.FullName;
        user.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(
                password
            );
        user.RoleId = role.Id;
        user.RoleName = role.Name;
        user.UserType = userType;
        user.ProfileId = profileId;
        user.IsActive = true;
        user.IsEmailConfirmed = true;
        user.MustChangePassword = true;
        user.TwoFactorEnabled = false;
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.IsDeleted = false;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = SeedUser;

        await _db.Users.ReplaceOneAsync(
            item => item.Id == user.Id,
            user
        );
    }

    private static Quote BuildQuote(
        Scenario scenario,
        CommercialPlan plan,
        CommercialPackage package,
        string status)
    {
        var subtotal = decimal.Round(
            package.Price * scenario.PackageQuantity,
            2
        );

        var discount = scenario.RecipientType == "Institution"
            ? decimal.Round(subtotal * 0.03m, 2)
            : 0m;

        var tax = decimal.Round(
            (subtotal - discount) * 0.16m,
            2
        );

        var recipientName = scenario.Customer?.FullName ??
            scenario.Institution!.Name;

        var contactName = scenario.Customer?.FullName ??
            scenario.Institution!.Responsible.Name.FullName;

        var email = scenario.Customer?.Email ??
            scenario.Institution!.Responsible.Email;

        var phone = scenario.Customer?.Phone ??
            scenario.Institution!.Responsible.Phone;

        return new Quote
        {
            Folio =
                $"QUO-DEMO-{scenario.CreatedAt:yyyyMMdd}-" +
                Guid.NewGuid().ToString("N")[..5].ToUpperInvariant(),
            RecipientType = scenario.RecipientType,
            CustomerId = scenario.Customer?.Id,
            InstitutionId = scenario.Institution?.Id,
            RecipientName = recipientName,
            ContactName = contactName,
            Email = email,
            Phone = phone,
            CommercialPlanId = plan.Id,
            CommercialPlanName = plan.Name,
            CommercialPackageId = package.Id,
            CommercialPackageName = package.Name,
            PackageQuantity = scenario.PackageQuantity,
            PackageUnitPrice = package.Price,
            Details = package.Items.Select(item =>
                new QuoteDetail
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    QuantityPerPackage = item.Quantity,
                    TotalQuantity =
                        item.Quantity * scenario.PackageQuantity,
                    UnitPrice = item.UnitPrice,
                    Subtotal = decimal.Round(
                        item.Subtotal * scenario.PackageQuantity,
                        2
                    )
                }).ToList(),
            Subtotal = subtotal,
            Discount = discount,
            TaxRate = 0.16m,
            Tax = tax,
            Shipping = 0,
            Total = subtotal - discount + tax,
            ValidUntil = scenario.CreatedAt.AddDays(15),
            Conditions =
                "Precios expresados en MXN. Sujeto a disponibilidad.",
            Status = status,
            IsDeleted = false,
            CreatedAt = scenario.CreatedAt,
            CreatedBy = SeedUser
        };
    }

    private static Order BuildOrder(
        Scenario scenario,
        Quote quote)
    {
        var ready = scenario.OrderStatus is "Sold" or "ReadyForSale";

        return new Order
        {
            Folio =
                $"ORD-DEMO-{scenario.CreatedAt:yyyyMMdd}-" +
                Guid.NewGuid().ToString("N")[..5].ToUpperInvariant(),
            QuoteId = quote.Id,
            QuoteFolio = quote.Folio,
            RecipientType = quote.RecipientType,
            CustomerId = quote.CustomerId,
            InstitutionId = quote.InstitutionId,
            RecipientName = quote.RecipientName,
            ContactName = quote.ContactName,
            Email = quote.Email,
            Phone = quote.Phone,
            CommercialPlanId = quote.CommercialPlanId,
            CommercialPlanName = quote.CommercialPlanName,
            CommercialPackageId = quote.CommercialPackageId,
            CommercialPackageName = quote.CommercialPackageName,
            Status = scenario.OrderStatus,
            Subtotal = quote.Subtotal,
            Discount = quote.Discount,
            Tax = quote.Tax,
            Shipping = quote.Shipping,
            Total = quote.Total,
            Details = quote.Details.Select(item =>
                new OrderDetail
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    RequestedQuantity = item.TotalQuantity,
                    ReservedQuantity = ready
                        ? item.TotalQuantity
                        : 0,
                    PendingQuantity =
                        scenario.OrderStatus == "AwaitingProduction"
                            ? item.TotalQuantity
                            : 0,
                    UnitPrice = item.UnitPrice,
                    Subtotal = item.Subtotal
                }).ToList(),
            ConfirmedAt = scenario.OrderStatus == "PendingConfirmation"
                ? null
                : scenario.CreatedAt.AddDays(1),
            ReadyForSaleAt = ready
                ? scenario.CreatedAt.AddDays(1)
                : null,
            IsDeleted = false,
            CreatedAt = scenario.CreatedAt.AddDays(1),
            CreatedBy = SeedUser
        };
    }

    private static Sale BuildSale(
        Order order,
        DateTime saleDate)
    {
        return new Sale
        {
            Folio =
                $"SAL-DEMO-{saleDate:yyyyMMdd}-" +
                Guid.NewGuid().ToString("N")[..5].ToUpperInvariant(),
            OrderId = order.Id,
            OrderFolio = order.Folio,
            QuoteId = order.QuoteId,
            QuoteFolio = order.QuoteFolio,
            RecipientType = order.RecipientType,
            CustomerId = order.CustomerId,
            InstitutionId = order.InstitutionId,
            RecipientName = order.RecipientName,
            ContactName = order.ContactName,
            Email = order.Email,
            Phone = order.Phone,
            CommercialPlanId = order.CommercialPlanId,
            CommercialPlanName = order.CommercialPlanName,
            CommercialPackageId = order.CommercialPackageId,
            CommercialPackageName = order.CommercialPackageName,
            SaleDate = saleDate,
            Subtotal = order.Subtotal,
            Discount = order.Discount,
            Tax = order.Tax,
            Shipping = order.Shipping,
            Total = order.Total,
            Details = order.Details.Select(item =>
                new SaleDetail
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.RequestedQuantity,
                    UnitPrice = item.UnitPrice,
                    Subtotal = item.Subtotal
                }).ToList(),
            IsDeleted = false,
            CreatedAt = saleDate,
            CreatedBy = SeedUser
        };
    }

    private static List<License> BuildLicenses(
        Sale sale,
        Order order,
        CommercialPlan plan)
    {
        var licenses = new List<License>();

        foreach (var detail in sale.Details)
        {
            for (var index = 0; index < detail.Quantity; index++)
            {
                var active = index % 3 != 2;

                licenses.Add(
                    new License
                    {
                        LicenseCode =
                            $"VOLTS-DEMO-{sale.SaleDate:yyyyMMdd}-" +
                            Guid.NewGuid()
                                .ToString("N")[..8]
                                .ToUpperInvariant(),
                        SaleId = sale.Id,
                        SaleFolio = sale.Folio,
                        OrderId = order.Id,
                        OrderFolio = order.Folio,
                        SaleDetailId = detail.Id,
                        ProductId = detail.ProductId,
                        ProductName = detail.ProductName,
                        CommercialPlanId = order.CommercialPlanId,
                        CommercialPlanName = order.CommercialPlanName,
                        CommercialPackageId = order.CommercialPackageId,
                        CommercialPackageName = order.CommercialPackageName,
                        RecipientType = order.RecipientType,
                        CustomerId = order.CustomerId,
                        InstitutionId = order.InstitutionId,
                        RecipientName = order.RecipientName,
                        Status = active
                            ? "Active"
                            : "Available",
                        WarrantyStartDate = sale.SaleDate,
                        WarrantyEndDate = sale.SaleDate
                            .AddMonths(plan.WarrantyMonths),
                        ActivationDate = active
                            ? sale.SaleDate
                            : null,
                        AssignedToName = active
                            ? order.ContactName
                            : null,
                        AssignedToEmail = active
                            ? order.Email
                            : null,
                        DeviceSerialNumber = active
                            ? $"VOLTS-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}"
                            : null,
                        IsDeleted = false,
                        CreatedAt = sale.SaleDate,
                        CreatedBy = SeedUser
                    }
                );
            }
        }

        return licenses;
    }

    private static Comment BuildComment(
        string fullName,
        string email,
        string message,
        int rating,
        bool approved,
        DateTime createdAt)
    {
        return new Comment
        {
            FullName = fullName,
            Email = email,
            Message = message,
            Rating = rating,
            IsApproved = approved,
            IsDeleted = false,
            CreatedAt = createdAt,
            CreatedBy = SeedUser
        };
    }

    private static EtlLog BuildEtl(
        string processName,
        int read,
        int processed,
        int rejected,
        string status,
        DateTime startedAt)
    {
        return new EtlLog
        {
            ProcessName = processName,
            Source = "MongoDB empresarial",
            Destination = "Dashboard y analítica",
            Status = status,
            StartedAt = startedAt,
            FinishedAt = startedAt.AddSeconds(4),
            RecordsRead = read,
            RecordsProcessed = processed,
            RecordsRejected = rejected,
            Phases = new List<string>
            {
                "Selección",
                "Preprocesamiento",
                "Minería descriptiva",
                "Interpretación",
                "Difusión"
            },
            Findings = new List<string>
            {
                "Datos normalizados correctamente.",
                "Indicadores difundidos al dashboard."
            },
            IsDeleted = false,
            CreatedAt = startedAt,
            CreatedBy = SeedUser
        };
    }

    private static AuditLog BuildAudit(
        string module,
        string action,
        string entityType,
        string description,
        DateTime createdAt)
    {
        return new AuditLog
        {
            UserName = "Sistema VOLTS",
            RoleName = "System",
            Module = module,
            Action = action,
            EntityType = entityType,
            Description = description,
            HttpMethod = "SEED",
            Path = "/seed/commercial-demo",
            StatusCode = 200,
            CorrelationId =
                Guid.NewGuid().ToString("N"),
            IsDeleted = false,
            CreatedAt = createdAt,
            CreatedBy = SeedUser
        };
    }

    private sealed record Scenario(
        string RecipientType,
        Customer? Customer,
        Institution? Institution,
        string PackageCode,
        int PackageQuantity,
        string OrderStatus,
        DateTime CreatedAt
    );
}
