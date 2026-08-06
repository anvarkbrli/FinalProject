namespace Ludora.Persistence.Services
{
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using System.Text;
    using Ludora.Application.DTOs.JWT;
    using Ludora.Application.Interfaces;
    using Ludora.Application.Settings;
    using Ludora.Domain.Entities.User;
    using Microsoft.Extensions.Options;
    using Microsoft.IdentityModel.Tokens;

    public class JwtTokenService : IJwtTokenService
    {
        private readonly JwtSettings _settings;

        public JwtTokenService(IOptions<JwtSettings> options)
        {
            _settings = options.Value;
        }

        public JwtTokenDTO CreateToken(AppUser user, IEnumerable<string> roles, Guid sessionId)
        {
            ValidateSettings();

            var createdAtUtc = DateTime.UtcNow;
            var expiresAtUtc = createdAtUtc.AddMinutes(_settings.ExpiresInMinutes);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim("security_stamp", user.SecurityStamp ?? string.Empty),
                new Claim("session_id", sessionId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                notBefore: createdAtUtc,
                expires: expiresAtUtc,
                signingCredentials: credentials
            );

            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

            return new JwtTokenDTO
            {
                AccessToken = accessToken,
                ExpiresAtUtc = expiresAtUtc
            };
        }

        private void ValidateSettings()
        {
            if (string.IsNullOrWhiteSpace(_settings.Key))
                throw new InvalidOperationException("JwtSettings:Key boş ola bilməz.");

            if (_settings.Key.Length < 32)
                throw new InvalidOperationException("JwtSettings:Key ən azı 32 simvol olmalıdır.");

            if (string.IsNullOrWhiteSpace(_settings.Issuer))
                throw new InvalidOperationException("JwtSettings:Issuer boş ola bilməz.");

            if (string.IsNullOrWhiteSpace(_settings.Audience))
                throw new InvalidOperationException("JwtSettings:Audience boş ola bilməz.");

            if (_settings.ExpiresInMinutes <= 0)
                throw new InvalidOperationException("JwtSettings:ExpiresInMinutes sıfırdan böyük olmalıdır.");
        }
    }
}
