using Volts.Api.Models;

namespace Volts.Api.Services;

public class PermissionCatalogService
{
    private static readonly HashSet<string> ProtectedRoleNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Admin",
            "Employee",
            "Client",
            "Institution"
        };

    private static readonly List<PermissionDefinition> Definitions =
    [
        Build(
            "administration.users.read",
            "Administración",
            "Consultar cuentas de acceso",
            "Permite consultar usuarios internos, clientes con cuenta e instituciones con cuenta.",
            "Admin"
        ),
        Build(
            "administration.users.manage",
            "Administración",
            "Administrar cuentas de acceso",
            "Permite crear, editar, activar, desbloquear y eliminar cuentas internas.",
            "Admin"
        ),
        Build(
            "administration.roles.read",
            "Administración",
            "Consultar roles",
            "Permite consultar roles y su matriz de permisos.",
            "Admin"
        ),
        Build(
            "administration.roles.manage",
            "Administración",
            "Administrar roles",
            "Permite crear, editar y eliminar roles.",
            "Admin"
        ),

        Build(
            "commercial.customers.read",
            "Comercial",
            "Consultar clientes e instituciones",
            "Permite consultar perfiles comerciales de clientes e instituciones.",
            "Admin",
            "Employee"
        ),
        Build(
            "commercial.customers.manage",
            "Comercial",
            "Administrar clientes e instituciones",
            "Permite crear, editar, activar y desactivar perfiles comerciales.",
            "Admin",
            "Employee"
        ),
        Build(
            "commercial.plans.manage",
            "Comercial",
            "Administrar planes y paquetes",
            "Permite administrar planes y paquetes comerciales.",
            "Admin",
            "Employee"
        ),
        Build(
            "commercial.quotes.manage",
            "Comercial",
            "Administrar cotizaciones",
            "Permite crear, aprobar, rechazar y convertir cotizaciones.",
            "Admin",
            "Employee"
        ),
        Build(
            "commercial.orders.manage",
            "Comercial",
            "Administrar pedidos",
            "Permite confirmar, reservar, sincronizar y cancelar pedidos.",
            "Admin",
            "Employee"
        ),
        Build(
            "commercial.sales.manage",
            "Comercial",
            "Administrar ventas",
            "Permite confirmar ventas desde pedidos listos.",
            "Admin",
            "Employee"
        ),
        Build(
            "commercial.licenses.manage",
            "Comercial",
            "Administrar licencias",
            "Permite asignar, activar y revocar licencias.",
            "Admin",
            "Employee"
        ),

        Build(
            "inventory.read",
            "Inventario",
            "Consultar inventario",
            "Permite consultar productos, materia prima, compras y movimientos.",
            "Admin",
            "Employee"
        ),
        Build(
            "inventory.manage",
            "Inventario",
            "Administrar inventario",
            "Permite registrar compras y movimientos autorizados.",
            "Admin",
            "Employee"
        ),
        Build(
            "production.read",
            "Producción",
            "Consultar producción",
            "Permite consultar recetas, órdenes y merma.",
            "Admin",
            "Employee"
        ),
        Build(
            "production.manage",
            "Producción",
            "Administrar producción",
            "Permite crear, iniciar, completar y cancelar producción.",
            "Admin",
            "Employee"
        ),

        Build(
            "support.read",
            "Atención",
            "Consultar atención y soporte",
            "Permite consultar contactos, comentarios y tickets.",
            "Admin",
            "Employee"
        ),
        Build(
            "support.manage",
            "Atención",
            "Administrar atención y soporte",
            "Permite responder y dar seguimiento a solicitudes.",
            "Admin",
            "Employee"
        ),
        Build(
            "content.manage",
            "Contenido",
            "Administrar contenido",
            "Permite gestionar comentarios, documentación y actualizaciones.",
            "Admin",
            "Employee"
        ),
        Build(
            "analytics.read",
            "Analítica",
            "Consultar dashboard y analítica",
            "Permite consultar indicadores y reportes.",
            "Admin",
            "Employee"
        ),

        Build(
            "portal.client.access",
            "Portal cliente",
            "Acceso al portal cliente",
            "Permite usar las funciones propias del portal individual.",
            "Client"
        ),
        Build(
            "portal.institution.access",
            "Portal institucional",
            "Acceso al portal institucional",
            "Permite usar las funciones propias del portal institucional.",
            "Institution"
        )
    ];

    public IReadOnlyList<PermissionDefinition> GetAll()
    {
        return Definitions;
    }

    public IReadOnlyList<PermissionDefinition> GetAvailableForRole(
        string roleName)
    {
        if (roleName.Equals(
                "Admin",
                StringComparison.OrdinalIgnoreCase))
        {
            return Definitions;
        }

        if (roleName.Equals(
                "Employee",
                StringComparison.OrdinalIgnoreCase))
        {
            return Definitions
                .Where(item =>
                    item.AllowedRoleNames.Contains(
                        "Employee",
                        StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        if (roleName.Equals(
                "Client",
                StringComparison.OrdinalIgnoreCase))
        {
            return Definitions
                .Where(item =>
                    item.AllowedRoleNames.Contains(
                        "Client",
                        StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        if (roleName.Equals(
                "Institution",
                StringComparison.OrdinalIgnoreCase))
        {
            return Definitions
                .Where(item =>
                    item.AllowedRoleNames.Contains(
                        "Institution",
                        StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        /*
         * Un rol personalizado es interno.
         * Puede usar permisos operativos, pero nunca administración crítica.
         */
        return Definitions
            .Where(item =>
                !item.Group.Equals(
                    "Administración",
                    StringComparison.OrdinalIgnoreCase) &&
                !item.Group.StartsWith(
                    "Portal",
                    StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public List<string> NormalizeForRole(
        string roleName,
        IEnumerable<string>? permissions)
    {
        if (roleName.Equals(
                "Admin",
                StringComparison.OrdinalIgnoreCase))
        {
            return ["*"];
        }

        var allowed = GetAvailableForRole(roleName)
            .Select(item => item.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return permissions?
            .Where(item =>
                !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Where(item => allowed.Contains(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToList()
            ?? [];
    }

    public bool IsProtectedRole(string roleName)
    {
        return ProtectedRoleNames.Contains(roleName);
    }

    private static PermissionDefinition Build(
        string code,
        string group,
        string label,
        string description,
        params string[] allowedRoleNames)
    {
        return new PermissionDefinition
        {
            Code = code,
            Group = group,
            Label = label,
            Description = description,
            AllowedRoleNames = allowedRoleNames.ToList()
        };
    }
}
