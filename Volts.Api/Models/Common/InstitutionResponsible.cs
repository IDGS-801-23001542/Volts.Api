namespace Volts.Api.Models.Common;

public class InstitutionResponsible
{
    public PersonName Name { get; set; } = new();
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Position { get; set; }
}
