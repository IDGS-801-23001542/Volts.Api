using Volts.Api.Models.Enums;

namespace Volts.Api.DTOs;

public class LoginRequestDto
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string FirstNames { get; set; } = string.Empty;

    public string PaternalLastName { get; set; } = string.Empty;

    public string? MaternalLastName { get; set; }

    public string Email { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public UserType UserType { get; set; }

    public string? ProfileId { get; set; }
}

public class CreateUserDto
{
    /*
     * Compatibilidad temporal con formularios anteriores.
     */
    public string? FullName { get; set; }

    public string FirstNames { get; set; } = string.Empty;

    public string PaternalLastName { get; set; } = string.Empty;

    public string? MaternalLastName { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string RoleName { get; set; } = "Employee";
}

public class RegisterClientDto
{
    /*
     * Se conserva hasta modificar el formulario Angular.
     */
    public string? FullName { get; set; }

    public string FirstNames { get; set; } = string.Empty;

    public string PaternalLastName { get; set; } = string.Empty;

    public string? MaternalLastName { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;

    public string? Phone { get; set; }
}