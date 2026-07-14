namespace Ludora.Persistence.Services;

using Ludora.Application.DTOs.JWT;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Mappings;
using Ludora.Domain.Entities.User;
using Ludora.Persistence.Extensions;
using Microsoft.AspNetCore.Identity;

public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        UserManager<AppUser> userManager,
        IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResponseDTO> RegisterAsync(RegisterDTO dto)
    {
        var email = dto.Email.Trim();
        var userName = dto.UserName.Trim();

        if (await _userManager.FindByEmailAsync(email) is not null)
            throw new ConflictException("Bu email artıq istifadə olunur.");

        if (await _userManager.FindByNameAsync(userName) is not null)
        {
            throw new ConflictException("Bu istifadəçi adı artıq istifadə olunur.");
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = dto.UserName.Trim(),
            Email = dto.Email.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, dto.Password);

        result.ThrowIfFailed();

        return await CreateAuthResponseAsync(user);
    }

    public async Task<AuthResponseDTO> LoginAsync(LoginDTO dto)
    {
        var value = dto.EmailOrUserName.Trim();

        var user = value.Contains('@') ? await _userManager.FindByEmailAsync(value) : await _userManager.FindByNameAsync(value);

        if (user is null)
        {
            throw new UnauthorizedException("Email, istifadəçi adı və ya şifrə yanlışdır.");
        }

        var passwordIsCorrect = await _userManager
            .CheckPasswordAsync(user, dto.Password);

        if (!passwordIsCorrect)
        {
            throw new UnauthorizedException("Email, istifadəçi adı və ya şifrə yanlışdır.");
        }

        return await CreateAuthResponseAsync(user);
    }

    private async Task<AuthResponseDTO> CreateAuthResponseAsync(AppUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenService.CreateToken(user, roles);

        return new AuthResponseDTO
        {
            AccessToken = token.AccessToken,
            ExpiresAtUtc = token.ExpiresAtUtc,
            User = user.ToCurrentUserDTO()
        };
    }
}