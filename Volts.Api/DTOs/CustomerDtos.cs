using Volts.Api.Models.Common;

namespace Volts.Api.DTOs;

public class CustomerCreateDto
{
    public string FirstNames { get; set; } = string.Empty;

    public string PaternalLastName { get; set; } = string.Empty;

    public string? MaternalLastName { get; set; }

    /*
     * Compatibilidad temporal con formularios antiguos.
     * Se utiliza solamente cuando los nombres estructurados
     * todavía no fueron enviados.
     */
    public string? FullName { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public Address? StructuredAddress { get; set; }

    /*
     * Dirección antigua en formato de texto.
     */
    public string? Address { get; set; }

    /*
     * Identificador del User relacionado.
     * Puede permanecer vacío mientras no se cree una cuenta de acceso.
     */
    public string? UserId { get; set; }

    /*
     * Campos antiguos conservados temporalmente.
     */
    public string? CustomerType { get; set; }

    public string? InstitutionName { get; set; }
}

public class CustomerUpdateDto : CustomerCreateDto
{
    public bool IsActive { get; set; } = true;
}