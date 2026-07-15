namespace Volts.Api.DTOs;

public class RoleCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
}

public class RoleUpdateDto : RoleCreateDto
{
    public bool IsActive { get; set; } = true;
}
