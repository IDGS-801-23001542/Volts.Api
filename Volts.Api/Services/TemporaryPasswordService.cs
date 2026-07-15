using System.Security.Cryptography;

namespace Volts.Api.Services;

public class TemporaryPasswordService
{
    private const string Characters =
        "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public string Generate()
    {
        Span<char> randomPart = stackalloc char[8];

        for (var index = 0; index < randomPart.Length; index++)
        {
            randomPart[index] =
                Characters[
                    RandomNumberGenerator.GetInt32(
                        Characters.Length
                    )
                ];
        }

        return $"VOLTS-{new string(randomPart)}!";
    }

    public List<string> Validate(string? password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add(
                "La contraseña temporal es obligatoria cuando no se genera automáticamente."
            );

            return errors;
        }

        if (password.Length < 8)
        {
            errors.Add(
                "La contraseña temporal debe tener al menos 8 caracteres."
            );
        }

        if (!password.Any(char.IsUpper))
        {
            errors.Add(
                "La contraseña temporal debe contener al menos una mayúscula."
            );
        }

        if (!password.Any(char.IsLower))
        {
            errors.Add(
                "La contraseña temporal debe contener al menos una minúscula."
            );
        }

        if (!password.Any(char.IsDigit))
        {
            errors.Add(
                "La contraseña temporal debe contener al menos un número."
            );
        }

        return errors;
    }
}
