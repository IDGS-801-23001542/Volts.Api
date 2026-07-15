using Volts.Api.Models.Common;
using Volts.Api.Models.Enums;

namespace Volts.Api.DTOs;

public class InstitutionCreateDto
{
    public string Name { get; set; } = string.Empty;

    public InstitutionType? Type { get; set; }

    /*
     * Compatibilidad con el formulario anterior,
     * que enviaba InstitutionType como texto.
     */
    public string? InstitutionType { get; set; }

    public string? EducationalLevel { get; set; }

    public int? EstimatedStudents { get; set; }

    public string? Website { get; set; }

    public InstitutionResponsible? Responsible { get; set; }

    /*
     * Compatibilidad con el formulario anterior.
     */
    public string? ContactName { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public Address? StructuredAddress { get; set; }

    /*
     * Dirección antigua en formato de texto.
     */
    public string? Address { get; set; }

    public string? UserId { get; set; }
}

public class InstitutionUpdateDto : InstitutionCreateDto
{
    public bool IsActive { get; set; } = true;
}