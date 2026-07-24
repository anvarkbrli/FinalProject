using System.Text;
using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Badge;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Helpers;
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
    private readonly IBadgeEvaluationQueue _badgeEvaluationQueue;

    public UserService(
        UserManager<AppUser> userManager,
        IFileStorageService fileStorageService,
        AppDbContext context,
        IEmailService emailService,
        IBadgeEvaluationQueue badgeEvaluationQueue,
        IOptions<EmailSettings> emailOptions)
    {
        _userManager = userManager;
        _fileStorageService = fileStorageService;
        _context = context;
        _emailService = emailService;
        _badgeEvaluationQueue = badgeEvaluationQueue;
        _emailSettings = emailOptions.Value;
    }

    public async Task<CurrentUserDTO> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdOrThrowAsync(userId);
        var summary = await GetProfileSummaryAsync(userId);

        return new CurrentUserDTO
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            Bio = user.Bio,
            ProfileImageUrl = user.ProfileImageUrl,
            SocialLinks = SocialLinkHelper.Deserialize(user.SocialLinksJson),
            IsPrivate = user.IsPrivate,
            CreatedAt = user.CreatedAt,
            FollowerCount = summary.FollowerCount,
            FollowingCount = summary.FollowingCount,
            PostCount = summary.PostCount,
            Statistics = summary.Statistics
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

        var summary = await GetProfileSummaryAsync(user.Id);
        var badges = await GetUserBadgesAsync(user.Id);
        _badgeEvaluationQueue.Enqueue(user.Id);

        return new UserProfileDTO
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Bio = user.Bio,
            ProfileImageUrl = user.ProfileImageUrl,
            SocialLinks = SocialLinkHelper.DeserializeWithAutomaticSteam(
                user.SocialLinksJson,
                user.SteamId,
                user.SteamProfileUrl),
            IsPrivate = user.IsPrivate,
            CreatedAt = user.CreatedAt,
            FollowerCount = summary.FollowerCount,
            FollowingCount = summary.FollowingCount,
            PostCount = summary.PostCount,
            Statistics = summary.Statistics,
            Badges = badges
        };
    }

    public async Task<CurrentUserDTO> UpdateProfileAsync(
        Guid userId,
        UpdateProfileDTO dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var user = await _userManager.FindByIdOrThrowAsync(userId);
        var userName = dto.UserName.Trim();
        var socialLinks = SocialLinkHelper.Normalize(dto.SocialLinks);
        var existingUser = await _userManager.FindByNameAsync(userName);

        if (existingUser is not null && existingUser.Id != userId)
            throw new ConflictException("Bu istifadəçi adı artıq istifadə olunur.");

        user.UserName = userName;
        user.Bio = string.IsNullOrWhiteSpace(dto.Bio)
            ? null
            : dto.Bio.Trim();
        user.SocialLinksJson = SocialLinkHelper.Serialize(socialLinks);

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

    public async Task<EmailOperationResultDTO> ChangePasswordAsync(
        Guid userId,
        ChangePasswordDTO dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.Equals(
                dto.CurrentPassword,
                dto.NewPassword,
                StringComparison.Ordinal))
        {
            throw new BadRequestException(
                "Yeni şifrə cari şifrədən fərqli olmalıdır.");
        }

        var user = await _userManager.FindByIdOrThrowAsync(userId);
        var result = await _userManager.ChangePasswordAsync(
            user,
            dto.CurrentPassword,
            dto.NewPassword);

        result.ThrowIfFailed();

        return new EmailOperationResultDTO
        {
            Message = "Şifrə uğurla dəyişdirildi."
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

    private async Task<ProfileSummary> GetProfileSummaryAsync(Guid userId)
    {
        var publicSummary = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                FollowerCount = x.Followers.Count(follow =>
                    follow.Status == FollowStatus.Accepted),
                FollowingCount = x.Following.Count(follow =>
                    follow.Status == FollowStatus.Accepted),
                PostCount = x.Posts.Count
            })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        var reviewSummary = await _context.Reviews
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(x => x.Content.Trim() != string.Empty),
                TotalPlaytimeMinutes = group.Sum(x => (long)(x.PlaytimeMinutes ?? 0))
            })
            .FirstOrDefaultAsync();

        var ratingGroups = await _context.UserGames
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Rating.HasValue)
            .GroupBy(x => x.Rating!.Value)
            .Select(group => new UserRatingDistributionDTO
            {
                Rating = group.Key,
                Count = group.Count()
            })
            .OrderBy(x => x.Rating)
            .ToListAsync();

        var ratingCount = ratingGroups.Sum(x => x.Count);
        var weightedRatingTotal = ratingGroups.Sum(x => x.Rating * x.Count);

        return new ProfileSummary(
            publicSummary.FollowerCount,
            publicSummary.FollowingCount,
            publicSummary.PostCount,
            new UserProfileStatisticsDTO
            {
                TotalPlaytimeMinutes = reviewSummary?.TotalPlaytimeMinutes ?? 0,
                ReviewCount = reviewSummary?.Count ?? 0,
                RatingCount = ratingCount,
                AverageRating = ratingCount == 0
                    ? null
                    : weightedRatingTotal / ratingCount,
                RatingDistribution = ratingGroups
            });
    }

    private Task<List<UserBadgeDTO>> GetUserBadgesAsync(Guid userId)
    {
        return _context.UserBadges
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.BadgeDefinition.IsActive)
            .OrderBy(x => x.BadgeDefinition.DisplayOrder)
            .ThenByDescending(x => x.AwardedAtUtc)
            .Select(x => new UserBadgeDTO
            {
                Id = x.BadgeDefinitionId,
                Code = x.BadgeDefinition.Code,
                Name = x.BadgeDefinition.Name,
                Description = x.BadgeDefinition.Description,
                Icon = x.BadgeDefinition.Icon,
                Color = x.BadgeDefinition.Color,
                MetricValueAtAward = x.MetricValueAtAward,
                AwardedAtUtc = x.AwardedAtUtc
            })
            .ToListAsync();
    }

    private sealed record ProfileSummary(
        int FollowerCount,
        int FollowingCount,
        int PostCount,
        UserProfileStatisticsDTO Statistics);

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
