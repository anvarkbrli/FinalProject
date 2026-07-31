namespace Ludora.Persistence.Services;

using System.Security.Cryptography;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class AuthService : IAuthService
{
    private const string VerificationPendingMessage =
        "Qeydiyyat tamamlandı, amma təsdiq emailini indi göndərmək mümkün olmadı. Hesabınız saxlanıldı; bir qədər sonra təsdiq emailini yenidən göndərə bilərsiniz.";

    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly EmailSettings _emailSettings;
    private readonly AuthSessionSettings _sessionSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<AppUser> userManager,
        IJwtTokenService jwtTokenService,
        AppDbContext context,
        IEmailService emailService,
        IOptions<EmailSettings> emailOptions,
        IOptions<AuthSessionSettings> sessionOptions,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _context = context;
        _emailService = emailService;
        _emailSettings = emailOptions.Value;
        _sessionSettings = sessionOptions.Value;
        _logger = logger;
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
                "Bu email artıq istifadə olunur. Hesab təsdiqlənməyibsə, təsdiq emailini yenidən göndər səhifəsindən istifadə edin.");
        }

        if (await _userManager.FindByNameAsync(userName) is not null)
            throw new ConflictException("Bu istifadəçi adı artıq istifadə olunur.");

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
                string.Join(", ", roleResult.Errors.Select(x => x.Description)));
        }

        try
        {
            await SendConfirmationEmailAsync(user, cancellationToken);

            return new RegisterResultDTO
            {
                Message = "Hesab yaradıldı. Təsdiq emaili göndəriş növbəsinə əlavə edildi. Email ünvanını təsdiqlədikdən sonra daxil ola bilərsiniz.",
                EmailVerificationRequired = true,
                EmailDeliveryPending = false
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Yeni yaradılmış {UserId} istifadəçisi üçün təsdiq emaili göndərilmədi. Hesab silinmədi.",
                user.Id);

            return new RegisterResultDTO
            {
                Message = VerificationPendingMessage,
                EmailVerificationRequired = true,
                EmailDeliveryPending = true
            };
        }
    }

    public async Task<AuthSessionResultDTO> LoginAsync(
        LoginDTO dto,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var value = dto.EmailOrUserName.Trim();
        var user = value.Contains('@')
            ? await _userManager.FindByEmailAsync(value)
            : await _userManager.FindByNameAsync(value);

        if (user is null)
            throw new UnauthorizedException("Email, istifadəçi adı və ya şifrə yanlışdır.");

        if (await _userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedException(
                "Çox sayda uğursuz giriş cəhdi edildi. Bir müddət sonra yenidən yoxlayın.");
        }

        var passwordIsCorrect = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!passwordIsCorrect)
        {
            var failedResult = await _userManager.AccessFailedAsync(user);
            failedResult.ThrowIfFailed();

            if (await _userManager.IsLockedOutAsync(user))
            {
                throw new UnauthorizedException(
                    "Çox sayda uğursuz giriş cəhdi edildi. Bir müddət sonra yenidən yoxlayın.");
            }

            throw new UnauthorizedException("Email, istifadəçi adı və ya şifrə yanlışdır.");
        }

        var resetResult = await _userManager.ResetAccessFailedCountAsync(user);
        resetResult.ThrowIfFailed();
        ValidateUserCanAuthenticate(user);

        return await CreateSessionAsync(
            user,
            ipAddress,
            userAgent,
            cancellationToken);
    }

    public async Task<AuthSessionResultDTO> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new UnauthorizedException("Sessiya məlumatı tapılmadı.");

        var tokenHash = HashToken(refreshToken);
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var session = await _context.RefreshSessions
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken)
            ?? throw new UnauthorizedException("Sessiya etibarlı deyil. Yenidən daxil olun.");

        var now = DateTime.UtcNow;
        if (session.RevokedAtUtc.HasValue)
        {
            if (session.ReplacedBySessionId.HasValue)
            {
                await RevokeAllActiveSessionsAsync(
                    session.UserId,
                    now,
                    ipAddress,
                    "Rotasiya olunmuş refresh token yenidən istifadə edildi.",
                    cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            throw new UnauthorizedException("Sessiya artıq etibarlı deyil. Yenidən daxil olun.");
        }

        if (session.ExpiresAtUtc <= now ||
            !string.Equals(
                session.SecurityStamp,
                session.User.SecurityStamp ?? string.Empty,
                StringComparison.Ordinal))
        {
            RevokeSession(session, now, ipAddress, "Sessiya müddəti bitib və ya təhlükəsizlik məlumatı dəyişib.");
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new UnauthorizedException("Sessiyanın müddəti bitib. Yenidən daxil olun.");
        }

        ValidateUserCanAuthenticate(session.User);

        var replacement = CreateRefreshSession(
            session.User,
            ipAddress,
            userAgent,
            session.SignedInAtUtc);
        session.RevokedAtUtc = now;
        session.RevokedByIp = NormalizeIp(ipAddress);
        session.RevocationReason = "Refresh token rotasiyası";
        session.ReplacedBySessionId = replacement.Entity.Id;
        _context.RefreshSessions.Add(replacement.Entity);

        await TrimOldSessionsAsync(session.UserId, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new UnauthorizedException("Sessiya artıq yenilənib. Yenidən daxil olun.");
        }

        return new AuthSessionResultDTO
        {
            Response = await CreateAuthResponseAsync(
                session.User,
                replacement.Entity.Id,
                cancellationToken),
            RefreshToken = replacement.RawToken,
            RefreshTokenExpiresAtUtc = replacement.Entity.ExpiresAtUtc
        };
    }

    public async Task LogoutAsync(
        string? refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        var tokenHash = HashToken(refreshToken);
        var session = await _context.RefreshSessions
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (session is null || session.RevokedAtUtc.HasValue)
            return;

        RevokeSession(session, DateTime.UtcNow, ipAddress, "İstifadəçi hesabdan çıxdı.");
        await _context.SaveChangesAsync(cancellationToken);
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

        if (user is null)
            throw new NotFoundException("Bu email ünvanı ilə qeydiyyatdan keçmiş hesab tapılmadı.");

        if (user.AccountDeletionRequestedAtUtc.HasValue)
            throw new ConflictException("Bu hesab üçün silinmə prosesi başladılıb. Yeni təsdiq emaili göndərilə bilməz.");

        var now = DateTime.UtcNow;
        if (user.IsPermanentlyBanned)
            throw new ForbiddenException("Bu hesab daimi banlanıb. Yeni təsdiq emaili göndərilə bilməz.");

        if (user.BannedUntilUtc.HasValue && user.BannedUntilUtc.Value > now)
        {
            var bannedUntilBaku = ConvertUtcToBakuTime(user.BannedUntilUtc.Value);
            throw new ForbiddenException(
                $"Bu hesab {bannedUntilBaku:dd.MM.yyyy} saat {bannedUntilBaku:HH:mm}-dək banlanıb. Yeni təsdiq emaili göndərilə bilməz.");
        }

        if (user.EmailConfirmed)
        {
            throw new ConflictException(
                "Bu email ünvanı artıq təsdiqlənib. Yeni təsdiq linkinə ehtiyac yoxdur; giriş səhifəsindən hesabınıza daxil olun.");
        }

        await SendConfirmationEmailAsync(user, cancellationToken);

        return new EmailOperationResultDTO
        {
            Message = "Təsdiq emaili göndəriş növbəsinə əlavə edildi. Bir neçə dəqiqə ərzində gəlməzsə Spam/Junk qovluğunu yoxlayın."
        };
    }

    public async Task<EmailOperationResultDTO> ForgotPasswordAsync(
        ForgotPasswordDTO dto,
        CancellationToken cancellationToken = default)
    {
        const string genericMessage =
            "Bu email ünvanına uyğun aktiv hesab varsa, şifrə yeniləmə linki göndərildi.";

        var email = dto.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null || !user.EmailConfirmed || user.AccountDeletionRequestedAtUtc.HasValue)
            return new EmailOperationResultDTO { Message = genericMessage };

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = EncodeToken(token);
        var resetLink = BuildPasswordResetLink(email, encodedToken);

        try
        {
            await _emailService.SendPasswordResetAsync(
                email,
                user.UserName ?? "WellPlayd istifadəçisi",
                resetLink,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "{UserId} istifadəçisi üçün şifrə yeniləmə emaili göndərilmədi.",
                user.Id);
        }

        return new EmailOperationResultDTO { Message = genericMessage };
    }

    public async Task<EmailOperationResultDTO> ResetPasswordAsync(
        ResetPasswordDTO dto,
        CancellationToken cancellationToken = default)
    {
        var email = dto.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email)
            ?? throw new BadRequestException("Şifrə yeniləmə linki etibarlı deyil və ya vaxtı bitib.");

        var decodedToken = DecodeToken(dto.Token);
        var result = await _userManager.ResetPasswordAsync(
            user,
            decodedToken,
            dto.NewPassword);

        if (!result.Succeeded)
        {
            var tokenError = result.Errors.Any(error =>
                string.Equals(error.Code, "InvalidToken", StringComparison.OrdinalIgnoreCase));

            if (tokenError)
                throw new BadRequestException("Şifrə yeniləmə linki etibarlı deyil və ya vaxtı bitib.");

            result.ThrowIfFailed();
        }

        await RevokeAllActiveSessionsAsync(
            user.Id,
            DateTime.UtcNow,
            null,
            "Şifrə yeniləndi.",
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new EmailOperationResultDTO
        {
            Message = "Şifrə uğurla yeniləndi. Yeni şifrə ilə hesaba daxil ola bilərsiniz."
        };
    }

    public async Task<IReadOnlyList<AuthDeviceSessionDTO>> GetActiveSessionsAsync(
        Guid userId,
        string? currentRefreshToken,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var currentTokenHash = string.IsNullOrWhiteSpace(currentRefreshToken)
            ? null
            : HashToken(currentRefreshToken);

        var currentSecurityStamp = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp ?? string.Empty)
            .SingleAsync(cancellationToken);

        var sessions = await _context.RefreshSessions
            .AsNoTracking()
            .Where(session =>
                session.UserId == userId &&
                session.SecurityStamp == currentSecurityStamp &&
                session.RevokedAtUtc == null &&
                session.ExpiresAtUtc > now)
            .OrderByDescending(session => session.LastActiveAtUtc)
            .ToListAsync(cancellationToken);

        return sessions
            .Select(session =>
            {
                var device = ParseDevice(session.UserAgent);
                return new AuthDeviceSessionDTO
                {
                    Id = session.Id,
                    DeviceName = device.DeviceName,
                    Browser = device.Browser,
                    OperatingSystem = device.OperatingSystem,
                    IpAddress = session.CreatedByIp,
                    SignedInAtUtc = session.SignedInAtUtc,
                    LastActiveAtUtc = session.LastActiveAtUtc,
                    ExpiresAtUtc = session.ExpiresAtUtc,
                    IsCurrent = currentTokenHash is not null &&
                                string.Equals(session.TokenHash, currentTokenHash, StringComparison.Ordinal)
                };
            })
            .ToList();
    }

    public async Task<RevokeAuthDeviceSessionResultDTO> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        string? currentRefreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.RefreshSessions
            .SingleOrDefaultAsync(
                item => item.Id == sessionId && item.UserId == userId,
                cancellationToken)
            ?? throw new NotFoundException("Sessiya tapılmadı.");

        var currentTokenHash = string.IsNullOrWhiteSpace(currentRefreshToken)
            ? null
            : HashToken(currentRefreshToken);
        var isCurrent = currentTokenHash is not null &&
                        string.Equals(session.TokenHash, currentTokenHash, StringComparison.Ordinal);

        if (!session.RevokedAtUtc.HasValue)
        {
            RevokeSession(
                session,
                DateTime.UtcNow,
                ipAddress,
                "İstifadəçi cihaz sessiyasını hesabdan çıxardı.");
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new RevokeAuthDeviceSessionResultDTO
        {
            CurrentSessionRevoked = isCurrent,
            Message = isCurrent
                ? "Bu cihaz hesabdan çıxarıldı."
                : "Seçilmiş cihaz hesabdan çıxarıldı."
        };
    }

    private async Task<AuthSessionResultDTO> CreateSessionAsync(
        AppUser user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var created = CreateRefreshSession(user, ipAddress, userAgent);
        _context.RefreshSessions.Add(created.Entity);
        await TrimOldSessionsAsync(user.Id, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new AuthSessionResultDTO
        {
            Response = await CreateAuthResponseAsync(
                user,
                created.Entity.Id,
                cancellationToken),
            RefreshToken = created.RawToken,
            RefreshTokenExpiresAtUtc = created.Entity.ExpiresAtUtc
        };
    }

    private (RefreshSession Entity, string RawToken) CreateRefreshSession(
        AppUser user,
        string? ipAddress,
        string? userAgent,
        DateTime? signedInAtUtc = null)
    {
        ValidateSessionSettings();

        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        var now = DateTime.UtcNow;
        var session = new RefreshSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            SecurityStamp = user.SecurityStamp ?? string.Empty,
            CreatedAtUtc = now,
            SignedInAtUtc = signedInAtUtc ?? now,
            LastActiveAtUtc = now,
            ExpiresAtUtc = now.AddDays(_sessionSettings.RefreshTokenLifetimeDays),
            CreatedByIp = NormalizeIp(ipAddress),
            UserAgent = NormalizeUserAgent(userAgent)
        };

        return (session, rawToken);
    }

    private async Task TrimOldSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var activeSessions = await _context.RefreshSessions
            .Where(x =>
                x.UserId == userId &&
                x.RevokedAtUtc == null &&
                x.ExpiresAtUtc > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(Math.Max(0, _sessionSettings.MaxActiveSessionsPerUser - 1))
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
        {
            RevokeSession(
                session,
                DateTime.UtcNow,
                null,
                "Aktiv sessiya limiti keçildi.");
        }
    }

    private async Task RevokeAllActiveSessionsAsync(
        Guid userId,
        DateTime revokedAtUtc,
        string? ipAddress,
        string reason,
        CancellationToken cancellationToken)
    {
        var sessions = await _context.RefreshSessions
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
            RevokeSession(session, revokedAtUtc, ipAddress, reason);
    }

    private static void RevokeSession(
        RefreshSession session,
        DateTime revokedAtUtc,
        string? ipAddress,
        string reason)
    {
        session.RevokedAtUtc = revokedAtUtc;
        session.RevokedByIp = NormalizeIp(ipAddress);
        session.RevocationReason = reason;
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
            user.UserName ?? "WellPlayd istifadəçisi",
            confirmationLink,
            cancellationToken);
    }

    private string BuildPasswordResetLink(string email, string token)
    {
        var endpoint = $"{_emailSettings.PublicFrontendBaseUrl.TrimEnd('/')}/reset-password";
        return QueryHelpers.AddQueryString(
            endpoint,
            new Dictionary<string, string?>
            {
                ["email"] = email,
                ["token"] = token
            });
    }

    private static (string DeviceName, string Browser, string OperatingSystem) ParseDevice(
        string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return ("Naməlum cihaz", "Naməlum brauzer", "Naməlum sistem");

        var browser = userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase)
            ? "Microsoft Edge"
            : userAgent.Contains("OPR/", StringComparison.OrdinalIgnoreCase)
                ? "Opera"
                : userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)
                    ? "Firefox"
                    : userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) ||
                      userAgent.Contains("CriOS/", StringComparison.OrdinalIgnoreCase)
                        ? "Chrome"
                        : userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase)
                            ? "Safari"
                            : "Naməlum brauzer";

        var operatingSystem = userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)
            ? "Android"
            : userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
              userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)
                ? "iOS/iPadOS"
                : userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase)
                    ? "Windows"
                    : userAgent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase)
                        ? "macOS"
                        : userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase)
                            ? "Linux"
                            : "Naməlum sistem";

        var deviceName = $"{browser} · {operatingSystem}";
        return (deviceName, browser, operatingSystem);
    }

    private string BuildConfirmationLink(Guid userId, string token)
    {
        var endpoint = $"{_emailSettings.PublicFrontendBaseUrl.TrimEnd('/')}/confirm-email";
        return QueryHelpers.AddQueryString(
            endpoint,
            new Dictionary<string, string?>
            {
                ["userId"] = userId.ToString(),
                ["token"] = token
            });
    }

    private static string EncodeToken(string token) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string DecodeToken(string token)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            throw new BadRequestException("Email təsdiq tokeni düzgün formatda deyil.");
        }
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string? NormalizeIp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= 64 ? trimmed : trimmed[..64];
    }

    private static string? NormalizeUserAgent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= 512 ? trimmed : trimmed[..512];
    }

    private void ValidateSessionSettings()
    {
        if (_sessionSettings.RefreshTokenLifetimeDays is < 1 or > 365)
            throw new InvalidOperationException("AuthSession:RefreshTokenLifetimeDays 1-365 aralığında olmalıdır.");

        if (_sessionSettings.MaxActiveSessionsPerUser is < 1 or > 100)
            throw new InvalidOperationException("AuthSession:MaxActiveSessionsPerUser 1-100 aralığında olmalıdır.");
    }

    private static void ValidateUserCanAuthenticate(AppUser user)
    {
        if (!user.EmailConfirmed)
            throw new UnauthorizedException("Hesabın email ünvanı təsdiqlənməyib. Emailinizdəki təsdiq linkinə keçin.");

        if (user.AccountDeletionRequestedAtUtc.HasValue)
        {
            throw new UnauthorizedException(
                "Hesabın silinməsi başladılıb. İcma sahiblik təklifləri tamamlandıqdan sonra hesab həmişəlik silinəcək.");
        }

        EnsureUserIsNotBanned(user);
    }

    private static void EnsureUserIsNotBanned(AppUser user)
    {
        var now = DateTime.UtcNow;
        var isBanned = user.IsPermanentlyBanned ||
                       (user.BannedUntilUtc.HasValue && user.BannedUntilUtc > now);

        if (!isBanned)
            return;

        if (user.IsPermanentlyBanned)
            throw new UnauthorizedException("Hesabınız admin tərəfindən müddətsiz banlanıb.");

        var bannedUntilBaku = ConvertUtcToBakuTime(user.BannedUntilUtc!.Value);
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
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenService.CreateToken(user, roles, sessionId);

        var userDto = user.ToCurrentUserDTO();
        userDto.Roles = roles.ToArray();
        userDto.Capabilities = RoleCapabilityMatrix.GetCapabilities(roles);
        var publicSummary = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == user.Id)
            .Select(x => new
            {
                FollowerCount = x.Followers.Count(follow => follow.Status == FollowStatus.Accepted),
                FollowingCount = x.Following.Count(follow => follow.Status == FollowStatus.Accepted),
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
