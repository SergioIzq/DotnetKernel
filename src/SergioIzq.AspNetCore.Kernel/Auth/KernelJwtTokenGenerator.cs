using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SergioIzq.AspNetCore.Kernel.Auth;

/// <summary>
/// Generador de tokens JWT genérico (HS256) a partir de <see cref="JwtSettings"/>.
/// Claims por defecto: sub, email, jti y NameIdentifier. Las apps con entidades de usuario
/// propias mantienen su interfaz específica como adapter fino sobre esta clase.
/// </summary>
public sealed class KernelJwtTokenGenerator
{
    private readonly JwtSettings _settings;

    public KernelJwtTokenGenerator(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public (string Token, DateTime ExpiresAt) GenerateToken(Guid userId, string email, IEnumerable<Claim>? extraClaims = null)
    {
        if (string.IsNullOrWhiteSpace(_settings.SecretKey))
        {
            throw new InvalidOperationException($"{JwtSettings.SectionName}:{nameof(JwtSettings.SecretKey)} no está configurada.");
        }

        var expirationTime = DateTime.UtcNow.AddMinutes(_settings.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };

        if (extraClaims != null)
        {
            claims.AddRange(extraClaims);
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expirationTime,
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return (tokenString, expirationTime);
    }
}
