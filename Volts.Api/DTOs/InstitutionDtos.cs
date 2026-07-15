using Volts.Api.Models.Enums;

namespace Volts.Api.DTOs;

public class InstitutionCreateDto : PortalAccountRequestDto
{
    public string Name { get; set; } = string.Empty;
    public InstitutionType InstitutionType { get; set; } = InstitutionType.Other;
    public InstitutionResponsibleDto Responsible { get; set; } = new();
    public AddressDto? Address { get; set; }
    public int? EstimatedStudents { get; set; }
    public string? Notes { get; set; }
}

public class InstitutionUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public InstitutionType InstitutionType { get; set; } = InstitutionType.Other;
    public InstitutionResponsibleDto Responsible { get; set; } = new();
    public AddressDto? Address { get; set; }
    public int? EstimatedStudents { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}
