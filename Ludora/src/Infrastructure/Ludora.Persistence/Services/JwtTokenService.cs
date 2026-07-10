namespace Ludora.Persistence.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Ludora.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(
        IOptions<JwtSettings> options
    )
    {
        _settings = options.Value;
    }

    public JwtTokenDTO CreateToken(
        AppUser user,
        IEnumerable<string> roles
    )
    {
        var createdAt = DateTime.UtcNow;

        var expiresAt = createdAt.AddMinutes(
            _settings.ExpireMinutes
        );

        var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                user.Id.ToString()
            ),
            new(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()
            ),
            new(
                ClaimTypes.Name,
                user.UserName ?? string.Empty
            ),
            new(
                JwtRegisteredClaimNames.Email,
                user.Email ?? string.Empty
            ),
            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString()
            )
        };

        foreach (var role in roles)
        {
            claims.Add(
                new Claim(
                    ClaimTypes.Role,
                    role
                )
            );
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                _settings.Key
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
            notBefore: createdAt,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtTokenDTO
        {
            AccessToken = new JwtSecurityTokenHandler()
                .WriteToken(token),

            ExpiresAtUtc = expiresAt
        };
    }
}