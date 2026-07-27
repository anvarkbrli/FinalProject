namespace Ludora.Persistence.Services;

using System.Text;
using Ludora.Application.Common;
using Ludora.Application.DTOs;
using Ludora.Application.DTOs.JWT;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Mappings;
using Ludora.Application.Settings;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Ludora.Persistence.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public class AuthService : IAuthService
{
    private const string VerificationMessage =
        "Qeydiyyat tamamlandı. Hesabı aktivləşdirmək üçün email ünvanınıza göndərilən təsdiq linkinə keçin.";

    private const string ResendMessage =
        "Bu email ilə təsdiqlənməmiş hesab mövcuddursa, yeni təsdiq linki göndərildi.";

    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly EmailSettings _emailSettings;

    public AuthService(
        UserManager<AppUser> userManager,
        IJwtTokenService jwtTokenService,
        AppDbContext context,
        IEmailService emailService,
        IOptions<EmailSettings> emailOptions)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _context = context;
        _emailService = emailService;
        _emailSettings = emailOptions.Value;
    }

    public async Task<RegisterResultDTO> RegisterAsync(
        RegisterDTO dto,
        CancellationToken cancellationToken = default)
    {
        var email = dto.Email.Trim();
        var userName = dto.UserName.Trim();

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException(
                "Bu email artıq istifadə olunur. Hesab təsdiqlənməyibsə, təsdiq emailini yenidən göndər endpoint-indən istifadə edin.");
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
            EmailConfirmed = false,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        result.ThrowIfFailed();

        var roleResult = await _userManager.AddToRoleAsync(user, RoleNames.User);

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);

            throw new InvalidOperationException(
                "İstifadəçiyə User rolu verilə bilmədi: " +
                string.Join(
                    ", ",
                    roleResult.Errors.Select(x => x.Description)));
        }

        try
        {
            await SendConfirmationEmailAsync(user, cancellationToken);
        }
        catch
        {
            await _userManager.DeleteAsync(user);
            throw;
        }

        return new RegisterResultDTO
        {
            Message = VerificationMessage,
            EmailVerificationRequired = true
        };
    }

    public async Task<AuthResponseDTO> LoginAsync(
        LoginDTO dto,
        CancellationToken cancellationToken = default)
    {
        var value = dto.EmailOrUserName.Trim();

        var user = value.Contains('@')
            ? await _userManager.FindByEmailAsync(value)
            : await _userManager.FindByNameAsync(value);

        if (user is null)
        {
            throw new UnauthorizedException(
                "Email, istifadəçi adı və ya şifrə yanlışdır.");
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedException(
                "Çox sayda uğursuz giriş cəhdi edildi. Bir müddət sonra yenidən yoxlayın.");
        }

        var passwordIsCorrect = await _userManager.CheckPasswordAsync(
            user,
            dto.Password);

        if (!passwordIsCorrect)
        {
            var failedResult = await _userManager.AccessFailedAsync(user);
            failedResult.ThrowIfFailed();

            if (await _userManager.IsLockedOutAsync(user))
            {
                throw new UnauthorizedException(
                    "Çox sayda uğursuz giriş cəhdi edildi. Bir müddət sonra yenidən yoxlayın.");
            }

            throw new UnauthorizedException(
                "Email, istifadəçi adı və ya şifrə yanlışdır.");
        }

        var resetResult = await _userManager.ResetAccessFailedCountAsync(user);
        resetResult.ThrowIfFailed();

        if (!user.EmailConfirmed)
        {
            throw new UnauthorizedException(
                "Hesabın email ünvanı təsdiqlənməyib. Emailinizdəki təsdiq linkinə keçin.");
        }

        if (user.AccountDeletionRequestedAtUtc.HasValue)
        {
            throw new UnauthorizedException(
                "Hesabın silinməsi başladılıb. İcma sahiblik təklifləri tamamlandıqdan sonra hesab həmişəlik silinəcək.");
        }

        EnsureUserIsNotBanned(user);

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<EmailOperationResultDTO> ConfirmEmailAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(token))
            throw new BadRequestException("Email təsdiq məlumatları düzgün deyil.");

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        if (user.EmailConfirmed)
        {
            return new EmailOperationResultDTO
            {
                Message = "Email ünvanı artıq təsdiqlənib. Hesaba daxil ola bilərsiniz."
            };
        }

        var decodedToken = DecodeToken(token);
        var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
        result.ThrowIfFailed();

        var stampResult = await _userManager.UpdateSecurityStampAsync(user);
        stampResult.ThrowIfFailed();

        return new EmailOperationResultDTO
        {
            Message = "Email ünvanı uğurla təsdiqləndi. İndi hesaba daxil ola bilərsiniz."
        };
    }

    public async Task<EmailOperationResultDTO> ResendEmailConfirmationAsync(
        ResendEmailConfirmationDTO dto,
        CancellationToken cancellationToken = default)
    {
        var email = dto.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);

        if (user is not null && !user.EmailConfirmed)
            await SendConfirmationEmailAsync(user, cancellationToken);

        return new EmailOperationResultDTO
        {
            Message = ResendMessage
        };
    }

    private async Task SendConfirmationEmailAsync(
        AppUser user,
        CancellationToken cancellationToken)
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = EncodeToken(token);
        var confirmationLink = BuildConfirmationLink(user.Id, encodedToken);

        await _emailService.SendEmailConfirmationAsync(
            user.Email ?? throw new InvalidOperationException("İstifadəçi emaili tapılmadı."),
            user.UserName ?? "Ludora istifadəçisi",
            confirmationLink,
            cancellationToken);
    }

    private string BuildConfirmationLink(Guid userId, string token)
    {
        var endpoint =
            $"{_emailSettings.PublicFrontendBaseUrl.TrimEnd('/')}/confirm-email";

        return QueryHelpers.AddQueryString(
            endpoint,
            new Dictionary<string, string?>
            {
                ["userId"] = userId.ToString(),
                ["token"] = token
            });
    }

    private static string EncodeToken(string token)
    {
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
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
                "Email təsdiq tokeni düzgün formatda deyil.");
        }
    }

    private static void EnsureUserIsNotBanned(AppUser user)
    {
        var now = DateTime.UtcNow;
        var isBanned = user.IsPermanentlyBanned ||
                       (user.BannedUntilUtc.HasValue &&
                        user.BannedUntilUtc > now);

        if (!isBanned)
            return;

        if (user.IsPermanentlyBanned)
        {
            throw new UnauthorizedException(
                "Hesabınız admin tərəfindən müddətsiz banlanıb.");
        }

        var bannedUntilBaku = ConvertUtcToBakuTime(
            user.BannedUntilUtc!.Value);

        throw new UnauthorizedException(
            $"Hesabınız {bannedUntilBaku:dd.MM.yyyy} saat {bannedUntilBaku:HH:mm}-dək banlanıb.");
    }

    private static DateTime ConvertUtcToBakuTime(DateTime utcDateTime)
    {
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        var timeZoneId = OperatingSystem.IsWindows()
            ? "Azerbaijan Standard Time"
            : "Asia/Baku";
        var bakuTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        return TimeZoneInfo.ConvertTimeFromUtc(utc, bakuTimeZone);
    }

    private async Task<AuthResponseDTO> CreateAuthResponseAsync(
        AppUser user,
        CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenService.CreateToken(user, roles);

        var userDto = user.ToCurrentUserDTO();
        userDto.Roles = roles.ToArray();
        var publicSummary = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == user.Id)
            .Select(x => new
            {
                FollowerCount = x.Followers.Count(follow =>
                    follow.Status == FollowStatus.Accepted),
                FollowingCount = x.Following.Count(follow =>
                    follow.Status == FollowStatus.Accepted),
                PostCount = x.Posts.Count
            })
            .FirstAsync(cancellationToken);

        userDto.FollowerCount = publicSummary.FollowerCount;
        userDto.FollowingCount = publicSummary.FollowingCount;
        userDto.PostCount = publicSummary.PostCount;

        var reviewSummary = await _context.Reviews
            .AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(x => x.Content.Trim() != string.Empty),
                TotalPlaytimeMinutes = group.Sum(x => (long)(x.PlaytimeMinutes ?? 0))
            })
            .FirstOrDefaultAsync(cancellationToken);

        var ratingDistribution = await _context.UserGames
            .AsNoTracking()
            .Where(x => x.UserId == user.Id && x.Rating.HasValue)
            .GroupBy(x => x.Rating!.Value)
            .Select(group => new UserRatingDistributionDTO
            {
                Rating = group.Key,
                Count = group.Count()
            })
            .OrderBy(x => x.Rating)
            .ToListAsync(cancellationToken);

        var ratingCount = ratingDistribution.Sum(x => x.Count);
        userDto.Statistics = new UserProfileStatisticsDTO
        {
            TotalPlaytimeMinutes = reviewSummary?.TotalPlaytimeMinutes ?? 0,
            ReviewCount = reviewSummary?.Count ?? 0,
            RatingCount = ratingCount,
            AverageRating = ratingCount == 0
                ? null
                : ratingDistribution.Sum(x => x.Rating * x.Count) / ratingCount,
            RatingDistribution = ratingDistribution
        };

        return new AuthResponseDTO
        {
            AccessToken = token.AccessToken,
            ExpiresAtUtc = token.ExpiresAtUtc,
            User = userDto
        };
    }
}
