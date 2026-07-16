namespace Volts.Api.Models;

public class PermissionDefinition
{
    public string Code { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /*
     * Roles base para los que este permiso tiene sentido.
     * Los roles personalizados pueden usar cualquier permiso no administrativo.
     */
    public List<string> AllowedRoleNames { get; set; } = new();
}
