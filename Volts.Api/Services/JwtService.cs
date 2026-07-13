using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Volts.Api.Models;
using Volts.Api.Settings;

namespace Volts.Api.Services;

public class JwtService
{
    private readonly JwtSettings _settings;

    public JwtService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public string GenerateToken(User user)
    {
        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                user.Id
            ),
            new(
                ClaimTypes.Name,
                user.FullName
            ),
            new(
                ClaimTypes.Email,
                user.Email
            ),
            new(
                ClaimTypes.Role,
                user.RoleName
            ),
            new(
                "user_type",
                user.UserType.ToString()
            )
        };

        if (!string.IsNullOrWhiteSpace(user.ProfileId))
        {
            claims.Add(
                new Claim(
                    "profile_id",
                    user.ProfileId
                )
            );
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                _settings.SecretKey
            )
        );

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                _settings.ExpirationMinutes
            ),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}