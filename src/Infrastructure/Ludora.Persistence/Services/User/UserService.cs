using Ludora.Application.Common;
using System.Text;
using System.Text.Json;
using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Badge;
using Ludora.Application.DTOs.FpsEstimator;
using Ludora.Application.DTOs.Home;
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
        var summary = await GetProfileSummaryAsync(userId);
        var roles = await _userManager.GetRolesAsync(user);

        return new CurrentUserDTO
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Roles = roles.ToArray(),
            Capabilities = RoleCapabilityMatrix.GetCapabilities(roles),
            EmailConfirmed = user.EmailConfirmed,
            Bio = user.Bio,
            ProfileImageUrl = user.ProfileImageUrl,
            SocialLinks = SocialLinkHelper.Deserialize(user.SocialLinksJson),
            IsPrivate = user.IsPrivate,
            PreferredLanguage = user.PreferredLanguage,
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

        var normalizedUserName = _userManager.NormalizeName(userName.Trim());
        var user = await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.NormalizedUserName == normalizedUserName);

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

        var isOwnProfile = currentUserId.HasValue && currentUserId.Value == user.Id;
        var isAcceptedFollower = currentUserId.HasValue &&
            currentUserId.Value != user.Id &&
            await _context.UserFollows
                .AsNoTracking()
                .AnyAsync(follow =>
                    follow.FollowerId == currentUserId.Value &&
                    follow.FollowingId == user.Id &&
                    follow.Status == FollowStatus.Accepted);
        var canViewContent = !user.IsPrivate || isOwnProfile || isAcceptedFollower;

        var summary = await GetProfileSummaryAsync(user.Id);
        var badges = await GetUserBadgesAsync(user.Id);

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
            CanViewContent = canViewContent,
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

    public async Task<ForumInterestProfileDTO?> GetForumInterestsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var serialized = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.ForumInterestsJson)
            .FirstOrDefaultAsync(cancellationToken);

        if (serialized is null)
        {
            var exists = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId, cancellationToken);

            if (!exists)
                throw new NotFoundException("İstifadəçi tapılmadı.");

            return null;
        }

        return DeserializeForumInterestState(serialized).Profile;
    }

    public async Task<ForumInterestProfileDTO> UpdateForumInterestsAsync(
        Guid userId,
        UpdateForumInterestProfileDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var styles = NormalizeInterestValues(dto.Styles, "Oyun tərzi", 6, 40);
        var genres = NormalizeInterestValues(dto.Genres, "Janr", 12, 100);
        var platform = NormalizeInterestValue(dto.Platform, "Platforma", 80);
        var games = (dto.Games ?? [])
            .Where(game => game is not null)
            .Select(game => new ForumInterestGameDTO
            {
                Key = NormalizeInterestValue(game.Key, "Oyun açarı", 120),
                Title = NormalizeInterestValue(game.Title, "Oyun adı", 200)
            })
            .DistinctBy(game => game.Key, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();

        if (styles.Length == 0)
            throw new BadRequestException("Ən azı bir oyun tərzi seçilməlidir.");
        if (games.Length < 3)
            throw new BadRequestException("Ən azı üç oyun seçilməlidir.");
        if (genres.Length == 0)
            throw new BadRequestException("Ən azı bir janr seçilməlidir.");

        var profile = new ForumInterestProfileDTO
        {
            Styles = styles,
            Games = games,
            Genres = genres,
            Platform = platform,
            CompletedAtUtc = DateTime.UtcNow
        };

        var user = await _userManager.FindByIdOrThrowAsync(userId);
        var state = DeserializeForumInterestState(user.ForumInterestsJson);
        user.ForumInterestsJson = JsonSerializer.Serialize(
            state with
            {
                Profile = profile,
                HomeRecommendations = null
            });

        var result = await _userManager.UpdateAsync(user);
        result.ThrowIfFailed();

        return profile;
    }

    public async Task<HomeInterestRecommendationsDTO?>
        GetSavedHomeInterestRecommendationsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
    {
        var serialized = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.ForumInterestsJson)
            .FirstOrDefaultAsync(cancellationToken);

        if (serialized is null)
        {
            var exists = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId, cancellationToken);

            if (!exists)
                throw new NotFoundException("İstifadəçi tapılmadı.");

            return null;
        }

        return DeserializeForumInterestState(serialized)
            .HomeRecommendations;
    }

    public async Task<HomeInterestRecommendationsDTO>
        SaveHomeInterestRecommendationsOnceAsync(
            Guid userId,
            HomeInterestRecommendationsDTO recommendations,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recommendations);

        var user = await _userManager.FindByIdOrThrowAsync(userId);
        var state = DeserializeForumInterestState(user.ForumInterestsJson);

        if (state.HomeRecommendations is { Items.Count: > 0 } existing &&
            string.Equals(
                existing.Language,
                recommendations.Language,
                StringComparison.OrdinalIgnoreCase))
        {
            return existing;
        }

        if (state.Profile is null)
            throw new BadRequestException("Maraq seçimləri tamamlanmayıb.");

        user.ForumInterestsJson = JsonSerializer.Serialize(
            state with { HomeRecommendations = recommendations });

        var result = await _userManager.UpdateAsync(user);
        result.ThrowIfFailed();

        return recommendations;
    }

    public async Task<CurrentUserDTO> UpdatePreferredLanguageAsync(
        Guid userId,
        UpdatePreferredLanguageDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var language = dto.Language.Trim().ToLowerInvariant();

        if (language is not ("az" or "en"))
            throw new BadRequestException("Dil yalnız az və ya en ola bilər.");

        var user = await _userManager.FindByIdOrThrowAsync(userId);
        user.PreferredLanguage = language;

        var result = await _userManager.UpdateAsync(user);
        result.ThrowIfFailed();

        return await GetCurrentUserAsync(userId);
    }

    public async Task<FpsHardwareProfileDTO> GetFpsHardwareProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new FpsHardwareProfileDTO
            {
                IsConfigured =
                    user.FpsGpuName != null &&
                    user.FpsCpuName != null &&
                    user.FpsRamGb.HasValue,
                Gpu = user.FpsGpuName,
                Cpu = user.FpsCpuName,
                RamGb = user.FpsRamGb,
                VramGb = user.FpsVramGb,
                UpdatedAtUtc = user.FpsHardwareUpdatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        return profile ??
               throw new NotFoundException("İstifadəçi tapılmadı.");
    }

    public async Task<FpsHardwareProfileDTO> UpdateFpsHardwareProfileAsync(
        Guid userId,
        UpdateFpsHardwareProfileDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var gpu = NormalizeHardwareName(dto.Gpu, "GPU");
        var cpu = NormalizeHardwareName(dto.Cpu, "CPU");

        if (dto.RamGb is < 1 or > 512)
            throw new BadRequestException("RAM 1-512 GB aralığında olmalıdır.");

        if (dto.VramGb is < 1 or > 128)
            throw new BadRequestException("VRAM 1-128 GB aralığında olmalıdır.");

        var user = await _context.Users
            .FirstOrDefaultAsync(
                candidate => candidate.Id == userId,
                cancellationToken)
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        var hasChanged =
            !string.Equals(
                user.FpsGpuName,
                gpu,
                StringComparison.Ordinal) ||
            !string.Equals(
                user.FpsCpuName,
                cpu,
                StringComparison.Ordinal) ||
            user.FpsRamGb != dto.RamGb ||
            user.FpsVramGb != dto.VramGb;

        if (hasChanged)
        {
            user.FpsGpuName = gpu;
            user.FpsCpuName = cpu;
            user.FpsRamGb = dto.RamGb;
            user.FpsVramGb = dto.VramGb;
            user.FpsHardwareUpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
        }

        return new FpsHardwareProfileDTO
        {
            IsConfigured = true,
            Gpu = user.FpsGpuName,
            Cpu = user.FpsCpuName,
            RamGb = user.FpsRamGb,
            VramGb = user.FpsVramGb,
            UpdatedAtUtc = user.FpsHardwareUpdatedAtUtc
        };
    }

    public async Task<EmailOperationResultDTO> ChangePasswordAsync(
        Guid userId,
        ChangePasswordDTO dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var user = await _userManager.FindByIdOrThrowAsync(userId);
        var hasPassword = await _userManager.HasPasswordAsync(user);

        IdentityResult result;
        if (hasPassword)
        {
            if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                throw new BadRequestException("Cari şifrəni daxil edin.");

            if (string.Equals(
                    dto.CurrentPassword,
                    dto.NewPassword,
                    StringComparison.Ordinal))
            {
                throw new BadRequestException(
                    "Yeni şifrə cari şifrədən fərqli olmalıdır.");
            }

            if (!await _userManager.CheckPasswordAsync(user, dto.CurrentPassword))
                throw new BadRequestException("Cari şifrə yanlışdır.");

            result = await _userManager.ChangePasswordAsync(
                user,
                dto.CurrentPassword,
                dto.NewPassword);
        }
        else
        {
            result = await _userManager.AddPasswordAsync(user, dto.NewPassword);
        }

        result.ThrowIfFailed();

        return new EmailOperationResultDTO
        {
            Message = hasPassword
                ? "Şifrə uğurla dəyişdirildi."
                : "Hesab üçün şifrə uğurla yaradıldı."
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
            throw new BadRequestException("Cari şifrə yanlışdır.");

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
            user.UserName ?? "WellPlayd istifadəçisi",
            confirmationLink,
            cancellationToken);

        return new EmailOperationResultDTO
        {
            Message =
                "Yeni email ünvanı üçün təsdiq məktubu göndəriş növbəsinə əlavə edildi. Link təsdiqlənənədək mövcud email dəyişməyəcək."
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

        var now = DateTime.UtcNow;
        var activeSessions = await _context.RefreshSessions
            .Where(session =>
                session.UserId == user.Id &&
                session.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
        {
            session.RevokedAtUtc = now;
            session.RevocationReason = "Email ünvanı dəyişdirildi.";
        }

        await _context.SaveChangesAsync(cancellationToken);

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

    private static string NormalizeHardwareName(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BadRequestException($"{fieldName} boş ola bilməz.");

        var normalized = string.Join(
            " ",
            value
                .Trim()
                .Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length > 150)
        {
            throw new BadRequestException(
                $"{fieldName} adı maksimum 150 simvol ola bilər.");
        }

        return normalized;
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
                IconUrl = x.BadgeDefinition.IconUrl,
                Color = x.BadgeDefinition.Color,
                MetricValueAtAward = x.MetricValueAtAward,
                AwardedAtUtc = x.AwardedAtUtc
            })
            .ToListAsync();
    }

    private static StoredForumInterestState DeserializeForumInterestState(
        string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
            return new StoredForumInterestState();

        try
        {
            var state = JsonSerializer.Deserialize<StoredForumInterestState>(
                serialized,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (state?.Profile is not null)
                return state;

            var legacyProfile =
                JsonSerializer.Deserialize<ForumInterestProfileDTO>(serialized);

            return new StoredForumInterestState
            {
                Profile = legacyProfile
            };
        }
        catch (JsonException)
        {
            return new StoredForumInterestState();
        }
    }

    private static string[] NormalizeInterestValues(
        IEnumerable<string>? values,
        string fieldName,
        int maxItems,
        int maxLength)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeInterestValue(value, fieldName, maxLength))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .ToArray();
    }

    private static string NormalizeInterestValue(
        string? value,
        string fieldName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BadRequestException($"{fieldName} boş ola bilməz.");

        var normalized = string.Join(
            " ",
            value.Trim().Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length > maxLength)
        {
            throw new BadRequestException(
                $"{fieldName} maksimum {maxLength} simvol ola bilər.");
        }

        return normalized;
    }

    private sealed record ProfileSummary(
        int FollowerCount,
        int FollowingCount,
        int PostCount,
        UserProfileStatisticsDTO Statistics);

    private sealed record StoredForumInterestState
    {
        public ForumInterestProfileDTO? Profile { get; init; }
        public HomeInterestRecommendationsDTO? HomeRecommendations { get; init; }
    }

    private string BuildEmailChangeConfirmationLink(
        Guid userId,
        string newEmail,
        string token)
    {
        var endpoint =
            $"{_emailSettings.PublicFrontendBaseUrl.TrimEnd('/')}/confirm-email-change";

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
