namespace Ludora.Persistence.Services;

using Ludora.Application.DTOs.JWT;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Mappings;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Ludora.Persistence.Extensions;
using Microsoft.AspNetCore.Identity;

public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly AppDbContext _context;
    private readonly SignInManager<AppUser> _signInManager;

    public AuthService(
        UserManager<AppUser> userManager,
        IJwtTokenService jwtTokenService,
        AppDbContext context,
        SignInManager<AppUser> signInManager)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _context = context;
        _signInManager = signInManager;
    }

    public async Task<AuthResponseDTO> RegisterAsync(RegisterDTO dto)
    {
        var email = dto.Email.Trim();
        var userName = dto.UserName.Trim();

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException(
                "Bu email artıq istifadə olunur.");
        }

        if (await _userManager.FindByNameAsync(userName) is not null)
        {
            throw new ConflictException(
                "Bu istifadəçi adı artıq istifadə olunur.");
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = email,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, dto.Password);

        result.ThrowIfFailed();

        var roleResult =
            await _userManager.AddToRoleAsync(user, "User");

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);

            throw new InvalidOperationException(
                "İstifadəçiyə User rolu verilə bilmədi: " +
                string.Join(
                    ", ",
                    roleResult.Errors.Select(x => x.Description)));
        }

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

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
            throw new UnauthorizedException("Çox sayda uğursuz giriş cəhdi edildi. Bir müddət sonra yenidən yoxlayın.");

        if (!signInResult.Succeeded)
            throw new UnauthorizedException("Email, istifadəçi adı və ya şifrə yanlışdır.");

        return await CreateAuthResponseAsync(user);
    }

    private async Task<AuthResponseDTO> CreateAuthResponseAsync(AppUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenService.CreateToken(user, roles);

        var userDto = user.ToCurrentUserDTO();
        userDto.FollowerCount = await _context.UserFollows.CountAsync(x =>
            x.FollowingId == user.Id &&
            x.Status == FollowStatus.Accepted);
        userDto.FollowingCount = await _context.UserFollows.CountAsync(x =>
            x.FollowerId == user.Id &&
            x.Status == FollowStatus.Accepted);
        userDto.PostCount = await _context.UserPosts.CountAsync(x => x.UserId == user.Id);

        return new AuthResponseDTO
        {
            AccessToken = token.AccessToken,
            ExpiresAtUtc = token.ExpiresAtUtc,
            User = userDto
        };
    }
}