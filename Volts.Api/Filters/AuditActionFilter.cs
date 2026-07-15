using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Volts.Api.Filters;

using Volts.Api.Services;

public class AuditActionFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> AuditedMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    private readonly AuditTrailService _audit;
    private readonly NotificationDispatchService _notifications;

    public AuditActionFilter(
        AuditTrailService audit,
        NotificationDispatchService notifications)
    {
        _audit = audit;
        _notifications = notifications;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var watch = Stopwatch.StartNew();
        var executed = await next();
        watch.Stop();

        var request = context.HttpContext.Request;
        if (!AuditedMethods.Contains(request.Method)) return;

        var controller = context.RouteData.Values["controller"]?.ToString() ?? "Unknown";
        if (controller is "Audit" or "SystemLogs" or "EtlLogs" or "Notifications" or "Auth") return;

        var routeAction = context.RouteData.Values["action"]?.ToString() ?? request.Method;
        var action = HumanizeAction(request.Method, routeAction);
        var responseData = ExtractResponse(executed.Result);
        var entityId = context.RouteData.Values["id"]?.ToString() ?? FindString(responseData, "id");
        var entityFolio = FindString(responseData, "folio");
        var statusCode = ResolveStatusCode(executed);
        var user = context.HttpContext.User;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue(ClaimTypes.Email);
        var role = user.FindFirstValue(ClaimTypes.Role);
        var isAuthenticated = user.Identity?.IsAuthenticated == true;
        var labels = ResolveLabels(controller);

        await _audit.WriteAsync(
            userId,
            userName,
            role,
            isAuthenticated ? "AuthenticatedUser" : "PublicVisitor",
            labels.Area,
            labels.Module,
            action,
            HumanizeEntity(controller),
            BuildDescription(userName, controller, action, entityFolio, statusCode),
            statusCode,
            request.Method,
            request.Path,
            context.HttpContext.TraceIdentifier,
            context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            request.Headers.UserAgent.ToString(),
            entityId,
            entityFolio,
            watch.ElapsedMilliseconds,
            context.ActionArguments,
            responseData);

        if (statusCode < 400)
        {
            try { await _notifications.NotifyBusinessEventAsync(controller, routeAction, entityId, entityFolio, userId); }
            catch { }
        }
    }

    private static int ResolveStatusCode(ActionExecutedContext executed)
    {
        if (executed.Exception != null) return 500;
        if (executed.Result is ObjectResult objectResult && objectResult.StatusCode.HasValue)
            return objectResult.StatusCode.Value;
        if (executed.Result is StatusCodeResult statusResult) return statusResult.StatusCode;
        return executed.HttpContext.Response.StatusCode == 0 ? 200 : executed.HttpContext.Response.StatusCode;
    }

    private static object? ExtractResponse(IActionResult? result) => result switch
    {
        ObjectResult value => value.Value,
        JsonResult value => value.Value,
        _ => null
    };

    private static string? FindString(object? value, string propertyName)
    {
        if (value == null) return null;
        try
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
            return Find(document.RootElement, propertyName);
        }
        catch { return null; }
    }

    private static string? Find(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                    return property.Value.GetString();
                var nested = Find(property.Value, name);
                if (nested != null) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = Find(item, name);
                if (nested != null) return nested;
            }
        }
        return null;
    }

    private static (string Area, string Module) ResolveLabels(string controller) => controller switch
    {
        "Users" or "Roles" => ("Administración", controller == "Users" ? "Cuentas de acceso" : "Roles y permisos"),
        "Customers" => ("Comercial", "Clientes"),
        "Institutions" => ("Comercial", "Instituciones"),
        "CommercialPlans" or "CommercialPackages" => ("Comercial", "Planes y paquetes"),
        "Quotes" => ("Comercial", "Cotizaciones"),
        "Orders" => ("Comercial", "Pedidos"),
        "Sales" => ("Comercial", "Ventas"),
        "Licenses" => ("Comercial", "Licencias"),
        "Products" => ("Producción e inventario", "Productos"),
        "Categories" => ("Producción e inventario", "Categorías"),
        "RawMaterials" => ("Producción e inventario", "Materia prima"),
        "Suppliers" => ("Producción e inventario", "Proveedores"),
        "Purchases" => ("Producción e inventario", "Compras"),
        "Recipes" => ("Producción e inventario", "Recetas BOM"),
        "Production" => ("Producción e inventario", "Producción"),
        "Waste" => ("Producción e inventario", "Merma"),
        "Comments" => ("Atención y contenido", "Comentarios"),
        "Contact" => ("Atención y contenido", "Contacto"),
        "SupportTickets" => ("Atención y contenido", "Soporte"),
        "Documentation" => ("Atención y contenido", "Documentación"),
        "UpdateNews" => ("Atención y contenido", "Actualizaciones"),
        _ => ("Sistema", controller)
    };

    private static string HumanizeAction(string method, string action)
    {
        if (action.Contains("Login", StringComparison.OrdinalIgnoreCase)) return "Iniciar sesión";
        if (action.Contains("Logout", StringComparison.OrdinalIgnoreCase)) return "Cerrar sesión";
        if (action.Contains("Approval", StringComparison.OrdinalIgnoreCase)) return "Cambiar aprobación";
        if (action.Contains("Status", StringComparison.OrdinalIgnoreCase)) return "Cambiar estado";
        if (action.Contains("Confirm", StringComparison.OrdinalIgnoreCase)) return "Confirmar";
        if (action.Contains("Synchronize", StringComparison.OrdinalIgnoreCase)) return "Sincronizar stock";
        if (action.Contains("Complete", StringComparison.OrdinalIgnoreCase)) return "Completar";
        if (action.Contains("Start", StringComparison.OrdinalIgnoreCase)) return "Iniciar";
        if (action.Contains("Cancel", StringComparison.OrdinalIgnoreCase)) return "Cancelar";
        if (action.Contains("Convert", StringComparison.OrdinalIgnoreCase)) return "Convertir";
        if (action.Contains("Assign", StringComparison.OrdinalIgnoreCase)) return "Asignar";
        if (action.Contains("Unlock", StringComparison.OrdinalIgnoreCase)) return "Desbloquear";
        if (action.Contains("Adjust", StringComparison.OrdinalIgnoreCase)) return "Ajustar inventario";
        return method.ToUpperInvariant() switch
        {
            "POST" => "Crear", "PUT" => "Actualizar", "PATCH" => "Actualizar", "DELETE" => "Eliminar", _ => action
        };
    }

    private static string HumanizeEntity(string controller) => controller switch
    {
        "Users" => "Usuario", "Roles" => "Rol", "Customers" => "Cliente", "Institutions" => "Institución",
        "Quotes" => "Cotización", "Orders" => "Pedido", "Sales" => "Venta", "Licenses" => "Licencia",
        "Products" => "Producto", "RawMaterials" => "Materia prima", "Purchases" => "Compra",
        "Production" => "Orden de producción", "Waste" => "Merma", "Comments" => "Comentario",
        "Contact" => "Mensaje de contacto", "SupportTickets" => "Ticket de soporte", _ => controller
    };

    private static string BuildDescription(string? userName, string controller, string action, string? folio, int statusCode)
    {
        var actor = string.IsNullOrWhiteSpace(userName) ? "Un visitante público" : userName;
        var entity = HumanizeEntity(controller).ToLowerInvariant();
        var reference = string.IsNullOrWhiteSpace(folio) ? string.Empty : $" {folio}";
        var result = statusCode >= 400 ? "La operación fue rechazada." : "La operación terminó correctamente.";
        return $"{actor} ejecutó la acción «{action}» sobre {entity}{reference}. {result}";
    }
}
