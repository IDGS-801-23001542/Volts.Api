using MongoDB.Driver;
using Volts.Api.Models;
using Volts.Api.Models.Common;
using Volts.Api.Models.Enums;

namespace Volts.Api.Services;

public class AdministrationClosureSeedService
{
    private const string SeedUser =
        "AdministrationClosureSeed";

    private readonly MongoDbService _db;

    public AdministrationClosureSeedService(
        MongoDbService db)
    {
        _db = db;
    }

    public async Task SeedAsync()
    {
        await SeedPortalAccountsAsync();
        await SeedCommentsAsync();
        await SeedAuditHistoryAsync();
    }

    private async Task SeedPortalAccountsAsync()
    {
        var clientRole = await _db.Roles
            .Find(item =>
                item.Name == "Client" &&
                item.IsActive &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        var institutionRole = await _db.Roles
            .Find(item =>
                item.Name == "Institution" &&
                item.IsActive &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (clientRole == null ||
            institutionRole == null)
        {
            return;
        }

        await CreateOrUpdateCustomerAccountAsync(
            "pancracio.lomito@volts.test",
            "VoltsPancracio2026!",
            clientRole
        );

        await CreateOrUpdateCustomerAccountAsync(
            "teofilo.croquetas@volts.test",
            "VoltsTeofilo2026!",
            clientRole
        );

        await CreateOrUpdateInstitutionAccountAsync(
            "Jardín de Niños Patitas del Saber",
            "VoltsPatitas2026!",
            institutionRole
        );

        await CreateOrUpdateInstitutionAccountAsync(
            "Kinder Pequeños Inventores del Bajío",
            "VoltsInventores2026!",
            institutionRole
        );
    }

    private async Task CreateOrUpdateCustomerAccountAsync(
        string email,
        string password,
        Role role)
    {
        var customer = await _db.Customers
            .Find(item =>
                item.Email == email &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (customer == null)
            return;

        var user = await _db.Users
            .Find(item =>
                item.Email == email &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            user = new User
            {
                Name = customer.Name,
                Email = customer.Email,
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        password
                    ),
                RoleId = role.Id,
                RoleName = role.Name,
                UserType = UserType.Customer,
                ProfileId = customer.Id,
                IsActive = true,
                IsEmailConfirmed = true,
                MustChangePassword = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            };

            await _db.Users.InsertOneAsync(user);
            return;
        }

        user.Name = customer.Name;
        user.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(
                password
            );
        user.RoleId = role.Id;
        user.RoleName = role.Name;
        user.UserType = UserType.Customer;
        user.ProfileId = customer.Id;
        user.IsActive = true;
        user.IsEmailConfirmed = true;
        user.MustChangePassword = true;
        user.IsDeleted = false;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = SeedUser;

        await _db.Users.ReplaceOneAsync(
            item => item.Id == user.Id,
            user
        );
    }

    private async Task CreateOrUpdateInstitutionAccountAsync(
        string institutionName,
        string password,
        Role role)
    {
        var institution =
            await _db.Institutions
                .Find(item =>
                    item.Name == institutionName &&
                    !item.IsDeleted)
                .FirstOrDefaultAsync();

        if (institution == null)
            return;

        var email =
            institution.Responsible.Email;

        var user = await _db.Users
            .Find(item =>
                item.Email == email &&
                !item.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            user = new User
            {
                Name =
                    institution.Responsible.Name,
                Email = email,
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        password
                    ),
                RoleId = role.Id,
                RoleName = role.Name,
                UserType =
                    UserType.Institution,
                ProfileId = institution.Id,
                IsActive = true,
                IsEmailConfirmed = true,
                MustChangePassword = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SeedUser
            };

            await _db.Users.InsertOneAsync(user);
            return;
        }

        user.Name =
            institution.Responsible.Name;
        user.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(
                password
            );
        user.RoleId = role.Id;
        user.RoleName = role.Name;
        user.UserType =
            UserType.Institution;
        user.ProfileId = institution.Id;
        user.IsActive = true;
        user.IsEmailConfirmed = true;
        user.MustChangePassword = true;
        user.IsDeleted = false;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = SeedUser;

        await _db.Users.ReplaceOneAsync(
            item => item.Id == user.Id,
            user
        );
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

        var comments = new List<Comment>
        {
            BuildComment(
                "Pancracio Lomito Pérez",
                "pancracio.lomito@volts.test",
                "VOLTS hizo que aprender programación fuera mucho más divertido en casa.",
                5,
                true
            ),
            BuildComment(
                "Teófilo Croquetas Ramírez",
                "teofilo.croquetas@volts.test",
                "La experiencia de compra fue clara y el robot llegó listo para comenzar.",
                5,
                true
            ),
            BuildComment(
                "Lupita Galletitas Mendoza",
                "lupita.galletitas@patitas.test",
                "El grupo se involucró rápidamente con las actividades del perro robot.",
                5,
                true
            ),
            BuildComment(
                "Tomás Tornillito Pérez",
                "tomas.tornillito@inventores.test",
                "La propuesta institucional facilita organizar prácticas de electrónica y programación.",
                4,
                true
            ),
            BuildComment(
                "Roberta Croquetina López",
                "roberta.croquetina@firulais.test",
                "Estamos evaluando ampliar el laboratorio con más unidades VOLTS.",
                4,
                false
            ),
            BuildComment(
                "Firulais Antonio Del Roble",
                "firulais.roble@volts.test",
                "Me gustaría contar con más guías para proyectos avanzados.",
                4,
                false
            )
        };

        await _db.Comments.InsertManyAsync(comments);
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

        var logs = new List<AuditLog>
        {
            BuildAudit(
                "Administración",
                "Crear",
                "Users",
                "Se generaron cuentas de acceso para clientes e instituciones.",
                now.AddDays(-8)
            ),
            BuildAudit(
                "Comercial",
                "Crear",
                "Quotes",
                "Se registraron cotizaciones comerciales de demostración.",
                now.AddDays(-7)
            ),
            BuildAudit(
                "Comercial",
                "Convertir",
                "Orders",
                "Una cotización fue convertida a pedido.",
                now.AddDays(-6)
            ),
            BuildAudit(
                "Producción e inventario",
                "Confirmar",
                "Production",
                "Se completó una orden de producción para cubrir faltantes.",
                now.AddDays(-5)
            ),
            BuildAudit(
                "Comercial",
                "Confirmar",
                "Sales",
                "Se confirmó una venta y se generaron licencias.",
                now.AddDays(-4)
            ),
            BuildAudit(
                "Atención y contenido",
                "Actualizar",
                "Comments",
                "Se aprobaron comentarios para el sitio público.",
                now.AddDays(-3)
            ),
            BuildAudit(
                "Administración",
                "Actualizar",
                "Roles",
                "Se actualizaron los permisos operativos del rol Employee.",
                now.AddDays(-2)
            )
        };

        await _db.AuditLogs.InsertManyAsync(logs);
    }

    private static Comment BuildComment(
        string fullName,
        string email,
        string message,
        int rating,
        bool approved)
    {
        return new Comment
        {
            FullName = fullName,
            Email = email,
            Message = message,
            Rating = rating,
            IsApproved = approved,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
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
            UserId = null,
            UserName = "Sistema VOLTS",
            RoleName = "System",
            Module = module,
            Action = action,
            EntityType = entityType,
            Description = description,
            HttpMethod = "SEED",
            Path = "/seed/administration-closure",
            StatusCode = 200,
            CorrelationId =
                Guid.NewGuid().ToString("N"),
            IsDeleted = false,
            CreatedAt = createdAt,
            CreatedBy = SeedUser
        };
    }
}
