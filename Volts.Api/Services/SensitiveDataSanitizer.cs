using System.Text.Json;
using System.Text.Json.Nodes;

namespace Volts.Api.Services;

public class SensitiveDataSanitizer
{
    private static readonly HashSet<string> SensitiveNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "password",
            "passwordHash",
            "currentPassword",
            "newPassword",
            "confirmPassword",
            "confirmNewPassword",
            "temporaryPassword",
            "token",
            "accessToken",
            "refreshToken",
            "authorization",
            "twoFactorSecret",
            "secretKey"
        };

    public string? Sanitize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var node = JsonNode.Parse(json);

            if (node == null)
                return null;

            SanitizeNode(node);

            var value = node.ToJsonString(
                new JsonSerializerOptions
                {
                    WriteIndented = false
                }
            );

            return value.Length > 12000
                ? value[..12000] + "…"
                : value;
        }
        catch
        {
            return "[Contenido no serializable]";
        }
    }

    private static void SanitizeNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (SensitiveNames.Contains(property.Key))
                {
                    obj[property.Key] = "[REDACTED]";
                    continue;
                }

                if (property.Value != null)
                    SanitizeNode(property.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item != null)
                    SanitizeNode(item);
            }
        }
    }
}
