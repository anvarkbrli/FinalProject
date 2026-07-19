using System.Text;
using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Ludora.Persistence.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ludora.Persistence.Services;

public class UserService : IUserService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IFileStorageService _fileStorageService;
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly EmailSettings _emailSettings;

    public UserService(
        UserManager<AppUser> userManager,
        IFileStorageService fileStorageService,
        AppDbContext context,
        IEmailService emailService,
        IOptions<EmailSettings> emailOptions)
    {
        _userManager = userManager;
        _fileStorageService = fileStorageService;
        _context = context;
        _emailService = emailService;
        _emailSettings = emailOptions.Value;
    }

    public async Task<CurrentUserDTO> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdOrThrowAsync(userId);

        var followerCount = await _context.UserFollows.CountAsync(
            uf => uf.FollowingId == userId &&
                  uf.Status == FollowStatus.Accepted);

        var followingCount = await _context.UserFollows.CountAsync(
            uf => uf.FollowerId == userId &&
                  uf.Status == FollowStatus.Accepted);

        return new CurrentUserDTO
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            Bio = user.Bio,
            ProfileImageUrl = user.ProfileImageUrl,
            IsPrivate = user.IsPrivate,
            CreatedAt = user.CreatedAt,
            FollowerCount = followerCount,
            FollowingCount = followingCount,
            PostCount = await _context.UserPosts.CountAsync(
                x => x.UserId == user.Id)
        };
    }

    public async Task<UserProfileDTO> GetByUserNameAsync(
        string userName,
        Guid? currentUserId)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new BadRequestException("İstifadəçi adı boş ola bilməz.");

        var user = await _userManager.Users
            .AsNoTracking()
            .Include(u => u.Followers)
            .Include(u => u.Following)
            .FirstOrDefaultAsync(
                u => u.NormalizedUserName == userName.Trim().ToUpper());

        if (user is null)
            throw new NotFoundException("İstifadəçi tapılmadı.");

        if (currentUserId.HasValue && currentUserId.Value != user.Id)
        {
            var hasBlock = await _context.UserBlocks.AnyAsync(ub =>
                ub.BlockerId == currentUserId.Value &&
                ub.BlockedUserId == user.Id ||
                ub.BlockerId == user.Id &&
                ub.BlockedUserId == currentUserId.Value);

            if (hasBlock)
                throw new ForbiddenException("Bu profilə baxmaq mümkün deyil.");
        }

        return new UserProfileDTO
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Bio = user.Bio,
            ProfileImageUrl = user.ProfileImageUrl,
            IsPrivate = user.IsPrivate,
            CreatedAt = user.CreatedAt,
            FollowerCount = await _context.UserFollows.CountAsync(
                uf => uf.FollowingId == user.Id &&
                      uf.Status == FollowStatus.Accepted),
            FollowingCount = await _context.UserFollows.CountAsync(
                uf => uf.FollowerId == user.Id &&
                      uf.Status == FollowStatus.Accepted),
            PostCount = await _context.UserPosts.CountAsync(
                x => x.UserId == user.Id)
        };
    }

    public async Task<CurrentUserDTO> UpdateProfileAsync(
        Guid userId,
        UpdateProfileDTO dto)
    {
        var user = await _userManager.FindByIdOrThrowAsync(userId);
        var userName = dto.UserName.Trim();

        var existingUser = await _userManager.FindByNameAsync(userName);

        if (existingUser is not null && existingUser.Id != userId)
            throw new ConflictException("Bu istifadəçi adı artıq istifadə olunur.");

        user.UserName = userName;
        user.Bio = string.IsNullOrWhiteSpace(dto.Bio)
            ? null
            : dto.Bio.Trim();

        var result = await _userManager.UpdateAsync(user);
        result.ThrowIfFailed();

        return await GetCurrentUserAsync(userId);
    }

    public async Task<CurrentUserDTO> UpdateProfileImageAsync(
        Guid userId,
        ProfileImageUploadDTO dto)
    {
        var user = await _userManager.FindByIdOrThrowAsync(userId);
        var oldImagePath = user.ProfileImageUrl;
        var newImagePath = await _fileStorageService.SaveProfileImageAsync(dto);

        user.ProfileImageUrl = newImagePath;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            await _fileStorageService.DeleteAsync(newImagePath);
            result.ThrowIfFailed();
        }

        await _fileStorageService.DeleteAsync(oldImagePath);

        return await GetCurrentUserAsync(userId);
    }

    public async Task<UserSettingsDTO> GetSettingsAsync(Guid userId)
    {
        var user = await _userManager.FindByIdOrThrowAsync(userId);

        return new UserSettingsDTO
        {
            IsPrivate = user.IsPrivate
        };
    }

    public async Task<UserSettingsDTO> UpdateSettingsAsync(
        Guid userId,
        UpdateUserSettingsDTO dto)
    {
        var user = await _userManager.FindByIdOrThrowAsync(userId);

        if (dto.IsPrivate.HasValue)
            user.IsPrivate = dto.IsPrivate.Value;

        var result = await _userManager.UpdateAsync(user);
        result.ThrowIfFailed();

        return new UserSettingsDTO
        {
            IsPrivate = user.IsPrivate
        };
    }

    public async Task<EmailOperationResultDTO> RequestEmailChangeAsync(
        Guid userId,
        ChangeEmailRequestDTO dto,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdOrThrowAsync(userId);
        var newEmail = dto.NewEmail.Trim();

        var passwordIsCorrect = await _userManager.CheckPasswordAsync(
            user,
            dto.CurrentPassword);

        if (!passwordIsCorrect)
            throw new UnauthorizedException("Cari şifrə yanlışdır.");

        var normalizedNewEmail = _userManager.NormalizeEmail(newEmail);

        if (string.Equals(
                normalizedNewEmail,
                user.NormalizedEmail,
                StringComparison.Ordinal))
        {
            throw new BadRequestException(
                "Yeni email mövcud email ünvanından fərqli olmalıdır.");
        }

        var existingUser = await _userManager.FindByEmailAsync(newEmail);

        if (existingUser is not null && existingUser.Id != userId)
            throw new ConflictException("Bu email artıq istifadə olunur.");

        var token = await _userManager.GenerateChangeEmailTokenAsync(
            user,
            newEmail);
        var encodedToken = EncodeToken(token);
        var confirmationLink = BuildEmailChangeConfirmationLink(
            user.Id,
            newEmail,
            encodedToken);

        await _emailService.SendEmailChangeConfirmationAsync(
            newEmail,
            user.UserName ?? "Ludora istifadəçisi",
            confirmationLink,
            cancellationToken);

        return new EmailOperationResultDTO
        {
            Message =
                "Yeni email ünvanına təsdiq linki göndərildi. Link təsdiqlənənədək mövcud email dəyişməyəcək."
        };
    }

    public async Task<EmailOperationResultDTO> ConfirmEmailChangeAsync(
        Guid userId,
        string newEmail,
        string token,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId == Guid.Empty ||
            string.IsNullOrWhiteSpace(newEmail) ||
            string.IsNullOrWhiteSpace(token))
        {
            throw new BadRequestException(
                "Email dəyişiklik təsdiq məlumatları düzgün deyil.");
        }

        var normalizedEmail = newEmail.Trim();
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);

        if (existingUser is not null && existingUser.Id != user.Id)
            throw new ConflictException("Bu email artıq istifadə olunur.");

        var decodedToken = DecodeToken(token);
        var result = await _userManager.ChangeEmailAsync(
            user,
            normalizedEmail,
            decodedToken);
        result.ThrowIfFailed();

        var stampResult = await _userManager.UpdateSecurityStampAsync(user);
        stampResult.ThrowIfFailed();

        return new EmailOperationResultDTO
        {
            Message =
                "Email ünvanı uğurla dəyişdirildi. Təhlükəsizlik səbəbilə bütün əvvəlki tokenlər etibarsız edildi; yeni email ilə yenidən daxil olun."
        };
    }

    private string BuildEmailChangeConfirmationLink(
        Guid userId,
        string newEmail,
        string token)
    {
        var endpoint =
            $"{_emailSettings.PublicApiBaseUrl.TrimEnd('/')}/api/users/email-change/confirm";

        return QueryHelpers.AddQueryString(
            endpoint,
            new Dictionary<string, string?>
            {
                ["userId"] = userId.ToString(),
                ["newEmail"] = newEmail,
                ["token"] = token
            });
    }

    private static string EncodeToken(string token)
    {
        return WebEncoders.Base64UrlEncode(
            Encoding.UTF8.GetBytes(token));
    }

    private static string DecodeToken(string token)
    {
        try
        {
            return Encoding.UTF8.GetString(
                WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            throw new BadRequestException(
                "Email dəyişiklik tokeni düzgün formatda deyil.");
        }
    }
}
