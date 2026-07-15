using MongoDB.Driver;
using Volts.Api.Models;

namespace Volts.Api.Services;

public class NotificationDispatchService
{
    private readonly MongoDbService _db;

    public NotificationDispatchService(MongoDbService db)
    {
        _db = db;
    }

    public async Task<int> NotifyRolesAsync(
        IEnumerable<string> roles,
        string title,
        string message,
        string type = "Information",
        string priority = "Normal",
        string module = "General",
        string? route = null,
        string? entityType = null,
        string? entityId = null,
        string? entityFolio = null,
        string? createdBy = null)
    {
        var roleSet = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var users = await _db.Users.Find(user =>
            !user.IsDeleted && user.IsActive && roleSet.Contains(user.RoleName))
            .ToListAsync();

        if (users.Count == 0) return 0;

        var notifications = users.Select(user => new Notification
        {
            UserId = user.Id,
            UserName = user.FullName,
            Title = title,
            Message = message,
            Type = type,
            Priority = priority,
            Module = module,
            Route = route,
            EntityType = entityType,
            EntityId = entityId,
            EntityFolio = entityFolio,
            IsRead = false,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        }).ToList();

        await _db.Notifications.InsertManyAsync(notifications);
        return notifications.Count;
    }

    public async Task NotifyUserAsync(
        string userId,
        string title,
        string message,
        string type = "Information",
        string priority = "Normal",
        string module = "General",
        string? route = null,
        string? entityType = null,
        string? entityId = null,
        string? entityFolio = null,
        string? createdBy = null)
    {
        var user = await _db.Users.Find(item =>
            item.Id == userId && !item.IsDeleted).FirstOrDefaultAsync();
        if (user == null) return;

        await _db.Notifications.InsertOneAsync(new Notification
        {
            UserId = user.Id,
            UserName = user.FullName,
            Title = title,
            Message = message,
            Type = type,
            Priority = priority,
            Module = module,
            Route = route,
            EntityType = entityType,
            EntityId = entityId,
            EntityFolio = entityFolio,
            IsRead = false,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        });
    }

    public async Task NotifyBusinessEventAsync(
        string controller,
        string action,
        string? entityId,
        string? entityFolio,
        string? actorUserId)
    {
        var definition = Resolve(controller, action, entityFolio);
        if (definition == null) return;

        await NotifyRolesAsync(
            definition.Roles,
            definition.Title,
            definition.Message,
            definition.Type,
            definition.Priority,
            definition.Module,
            definition.Route,
            controller,
            entityId,
            entityFolio,
            actorUserId);
    }

    private static Definition? Resolve(string controller, string action, string? folio)
    {
        var reference = string.IsNullOrWhiteSpace(folio) ? "registrado" : folio;
        return (controller, action) switch
        {
            ("Quotes", "Create") => new(new[]{"Admin","Employee"}, "Nueva cotización", $"Se recibió la cotización {reference}.", "Commercial", "Normal", "Comercial", "/backoffice/cotizaciones"),
            ("Orders", "Confirm") => new(new[]{"Admin","Employee"}, "Pedido confirmado", $"El pedido {reference} fue confirmado y requiere seguimiento de stock.", "Commercial", "High", "Comercial", "/backoffice/pedidos"),
            ("Orders", "SynchronizeStock") => new(new[]{"Admin","Employee"}, "Stock de pedido sincronizado", $"Se sincronizó el stock del pedido {reference}.", "Inventory", "Normal", "Comercial", "/backoffice/pedidos"),
            ("Sales", "Create") => new(new[]{"Admin","Employee"}, "Venta completada", $"Se registró la venta {reference} y se generaron sus licencias.", "Commercial", "Normal", "Comercial", "/backoffice/ventas"),
            ("Production", "Create") => new(new[]{"Admin","Employee"}, "Orden de producción creada", $"Se creó la orden de producción {reference}.", "Production", "Normal", "Producción e inventario", "/backoffice/produccion"),
            ("Production", "Complete") => new(new[]{"Admin","Employee"}, "Producción completada", $"La producción {reference} fue completada y actualizó inventario.", "Production", "Normal", "Producción e inventario", "/backoffice/produccion"),
            ("Purchases", "Create") => new(new[]{"Admin","Employee"}, "Compra registrada", $"Se registró la compra {reference} y se actualizó materia prima.", "Inventory", "Normal", "Producción e inventario", "/backoffice/compras"),
            ("Comments", "Create") => new(new[]{"Admin","Employee"}, "Comentario pendiente", "Se recibió un comentario que requiere aprobación.", "Content", "Normal", "Atención y contenido", "/backoffice/comentarios"),
            ("Contact", "Create") => new(new[]{"Admin","Employee"}, "Nuevo mensaje de contacto", "Se recibió un mensaje desde el sitio público.", "Support", "Normal", "Atención y contenido", "/backoffice/contacto"),
            ("SupportTickets", "Create") => new(new[]{"Admin","Employee"}, "Nuevo ticket de soporte", $"Se creó el ticket {reference}.", "Support", "High", "Atención y contenido", "/backoffice/soporte"),
            ("Users", "UpdateStatus") => new(new[]{"Admin"}, "Estado de usuario modificado", "Se modificó el estado de una cuenta de acceso.", "Security", "High", "Administración", "/backoffice/usuarios"),
            ("Roles", _) => new(new[]{"Admin"}, "Roles y permisos actualizados", "Se modificó la configuración de acceso del sistema.", "Security", "High", "Administración", "/backoffice/roles"),
            _ => null
        };
    }

    private sealed record Definition(
        string[] Roles, string Title, string Message, string Type,
        string Priority, string Module, string Route);
}
