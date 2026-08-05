namespace Ludora.Persistence.Services;

using System.Security.Cryptography;
using System.Text;
using Ludora.Application.Common;
using Ludora.Application.DTOs;
using Ludora.Application.DTOs.JWT;
using Ludora.Application.DTOs.ExternalAuth;
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
    private const string CurrentTermsVersion = "2026-08-01";
    private const string CurrentPrivacyVersion = "2026-08-01";
    private const string VerificationPendingMessage =
        "Qeydiyyat tamamlandı, amma təsdiq emailini indi göndərmək mümkün olmadı. Hesabınız saxlanıldı; bir qədər sonra təsdiq emailini yenidən göndərə bilərsiniz.";

    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly EmailSettings _emailSettings;
    private readonly AuthSessionSettings _sessionSettings;
    private readonly GoogleAuthenticationSettings _googleSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<AppUser> userManager,
        IJwtTokenService jwtTokenService,
        AppDbContext context,
        IEmailService emailService,
        IOptions<EmailSettings> emailOptions,
        IOptions<AuthSessionSettings> sessionOptions,
        IOptions<GoogleAuthenticationSettings> googleOptions,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _context = context;
        _emailService = emailService;
        _emailSettings = emailOptions.Value;
        _sessionSettings = sessionOptions.Value;
        _googleSettings = googleOptions.Value;
        _logger = logger;
    }


    public async Task<UsernameAvailabilityDTO> CheckUsernameAvailabilityAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = (userName ?? string.Empty).Trim();

        if (normalized.Length < 3 || normalized.Length > 30)
        {
            return new UsernameAvailabilityDTO
            {
                UserName = normalized,
                IsAvailable = false,
                Message = "İstifadəçi adı 3-30 simvol aralığında olmalıdır."
            };
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(
                normalized,
                @"^[a-zA-Z0-9_.]+$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            return new UsernameAvailabilityDTO
            {
                UserName = normalized,
                IsAvailable = false,
                Message = "Yalnız hərf, rəqəm, nöqtə və alt xətt istifadə edilə bilər."
            };
        }

        var existing = await _userManager.FindByNameAsync(normalized);
        return new UsernameAvailabilityDTO
        {
            UserName = normalized,
            IsAvailable = existing is null,
            Message = existing is null
                ? "Bu istifadəçi adı əlçatandır."
                : "Bu istifadəçi adı artıq istifadə olunur."
        };
    }

    public async Task<RegisterResultDTO> RegisterAsync(
        RegisterDTO dto,
        CancellationToken cancellationToken = default)
    {
        if (!dto.AcceptedTermsAndPrivacy)
            throw new BadRequestException("İstifadə şərtləri və məxfilik siyasəti ilə razılaşmalısınız.");

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
            CreatedAt = DateTime.UtcNow,
            TermsAcceptedAtUtc = DateTime.UtcNow,
            PrivacyAcceptedAtUtc = DateTime.UtcNow,
            TermsVersion = CurrentTermsVersion,
            PrivacyVersion = CurrentPrivacyVersion
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

    public async Task<ExternalAuthCallbackResultDTO> PrepareExternalLoginAsync(
        ExternalProviderProfileDTO profile,
        string returnPath,
        CancellationToken cancellationToken = default)
    {
        ValidateExternalProfile(profile);
        var normalizedReturnPath = NormalizeReturnPath(returnPath);

        var user = await _userManager.FindByLoginAsync(profile.Provider, profile.ProviderKey);
        if (user is not null)
        {
            await ValidateUserCanUseExternalLoginAsync(user);
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                var updateResult = await _userManager.UpdateAsync(user);
                updateResult.ThrowIfFailed();
            }
        }

        if (user is null)
        {
            user = await _userManager.FindByEmailAsync(profile.Email);
            if (user is not null)
            {
                await ValidateUserCanUseExternalLoginAsync(user);
                var existingLogins = await _userManager.GetLoginsAsync(user);
                var existingProviderLogin = existingLogins.FirstOrDefault(login =>
                    string.Equals(login.LoginProvider, profile.Provider, StringComparison.Ordinal));
                if (existingProviderLogin is not null &&
                    !string.Equals(existingProviderLogin.ProviderKey, profile.ProviderKey, StringComparison.Ordinal))
                {
                    throw new ConflictException(
                        "Bu WellPlayd hesabına başqa Google hesabı bağlıdır. Əvvəlcə onu ayırın.");
                }

                if (existingProviderLogin is null)
                {
                    var addLoginResult = await _userManager.AddLoginAsync(
                        user,
                        new UserLoginInfo(profile.Provider, profile.ProviderKey, profile.Provider));
                    if (!addLoginResult.Succeeded)
                    {
                        throw new ConflictException(
                            "Google hesabı başqa WellPlayd hesabına bağlıdır və ya bağlantı yaradıla bilmədi.");
                    }
                }

                var shouldUpdate = false;
                if (!user.EmailConfirmed)
                {
                    user.EmailConfirmed = true;
                    shouldUpdate = true;
                }

                if (string.IsNullOrWhiteSpace(user.ProfileImageUrl) &&
                    TryNormalizeHttpsUrl(profile.PictureUrl, out var pictureUrl))
                {
                    user.ProfileImageUrl = pictureUrl;
                    shouldUpdate = true;
                }

                if (shouldUpdate)
                {
                    var updateResult = await _userManager.UpdateAsync(user);
                    updateResult.ThrowIfFailed();
                }
            }
        }

        if (user is not null)
        {
            await ValidateExternalUserCanAuthenticateAsync(user);
            var code = await CreateExternalAuthTicketAsync(
                ExternalAuthTicketKind.LoginExchange,
                profile,
                user.Id,
                normalizedReturnPath,
                cancellationToken);

            return new ExternalAuthCallbackResultDTO
            {
                Code = code,
                ReturnPath = normalizedReturnPath,
                RequiresProfileCompletion = false
            };
        }

        var registrationCode = await CreateExternalAuthTicketAsync(
            ExternalAuthTicketKind.Registration,
            profile,
            null,
            normalizedReturnPath,
            cancellationToken);

        return new ExternalAuthCallbackResultDTO
        {
            Code = registrationCode,
            ReturnPath = normalizedReturnPath,
            RequiresProfileCompletion = true
        };
    }

    public async Task<ExternalAuthExchangeServiceResultDTO> ExchangeExternalAuthCodeAsync(
        string code,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var ticket = await GetValidExternalTicketAsync(code, cancellationToken);
        if (ticket.Kind == ExternalAuthTicketKind.Registration)
        {
            return new ExternalAuthExchangeServiceResultDTO
            {
                RequiresProfileCompletion = true,
                RegistrationToken = code,
                SuggestedUserName = await SuggestAvailableUserNameAsync(
                    ticket.DisplayName,
                    ticket.Email,
                    cancellationToken),
                Email = ticket.Email,
                DisplayName = ticket.DisplayName,
                PictureUrl = ticket.PictureUrl,
                ReturnPath = ticket.ReturnPath
            };
        }

        if (!ticket.UserId.HasValue)
            throw new BadRequestException("Google giriş kodu etibarlı deyil.");

        var user = await _userManager.FindByIdAsync(ticket.UserId.Value.ToString())
            ?? throw new UnauthorizedException("Google hesabına bağlı istifadəçi tapılmadı.");
        await ValidateExternalUserCanAuthenticateAsync(user);

        ticket.ConsumedAtUtc = DateTime.UtcNow;
        try
        {
            var session = await CreateSessionAsync(user, ipAddress, userAgent, cancellationToken);
            return new ExternalAuthExchangeServiceResultDTO
            {
                RequiresProfileCompletion = false,
                Session = session,
                ReturnPath = ticket.ReturnPath
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new UnauthorizedException("Google giriş kodu artıq istifadə olunub.");
        }
    }

    public async Task<ExternalRegistrationServiceResultDTO> CompleteExternalRegistrationAsync(
        CompleteExternalRegistrationDTO dto,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (!dto.AcceptedTermsAndPrivacy)
            throw new BadRequestException("İstifadə şərtləri və məxfilik siyasəti ilə razılaşmalısınız.");

        var userName = dto.UserName.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                userName,
                @"^[a-zA-Z0-9_.]{3,30}$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            throw new BadRequestException(
                "İstifadəçi adı 3-30 simvol olmalı və yalnız hərf, rəqəm, nöqtə və alt xətt saxlamalıdır.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var ticket = await GetValidExternalTicketAsync(dto.RegistrationToken, cancellationToken);
        if (ticket.Kind != ExternalAuthTicketKind.Registration)
            throw new BadRequestException("Google qeydiyyat kodu etibarlı deyil.");

        AppUser? user = await _userManager.FindByLoginAsync(ticket.Provider, ticket.ProviderKey);
        if (user is null)
        {
            user = await _userManager.FindByEmailAsync(ticket.Email);
            if (user is not null)
            {
                await ValidateUserCanUseExternalLoginAsync(user);
                if (!user.EmailConfirmed)
                {
                    user.EmailConfirmed = true;
                    var emailUpdateResult = await _userManager.UpdateAsync(user);
                    emailUpdateResult.ThrowIfFailed();
                }
                var existingLogins = await _userManager.GetLoginsAsync(user);
                var existingProviderLogin = existingLogins.FirstOrDefault(login =>
                    string.Equals(login.LoginProvider, ticket.Provider, StringComparison.Ordinal));
                if (existingProviderLogin is not null &&
                    !string.Equals(existingProviderLogin.ProviderKey, ticket.ProviderKey, StringComparison.Ordinal))
                {
                    throw new ConflictException(
                        "Bu WellPlayd hesabına başqa Google hesabı bağlıdır. Əvvəlcə onu ayırın.");
                }

                if (existingProviderLogin is null)
                {
                    var addLoginResult = await _userManager.AddLoginAsync(
                        user,
                        new UserLoginInfo(ticket.Provider, ticket.ProviderKey, ticket.Provider));
                    if (!addLoginResult.Succeeded)
                        throw new ConflictException("Google hesabı başqa hesaba bağlıdır.");
                }
            }
        }

        if (user is null)
        {
            if (await _userManager.FindByNameAsync(userName) is not null)
                throw new ConflictException("Bu istifadəçi adı artıq istifadə olunur.");

            user = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = userName,
                Email = ticket.Email,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                TermsAcceptedAtUtc = DateTime.UtcNow,
                PrivacyAcceptedAtUtc = DateTime.UtcNow,
                TermsVersion = CurrentTermsVersion,
                PrivacyVersion = CurrentPrivacyVersion,
                ProfileImageUrl = dto.UseGoogleProfileImage &&
                                  TryNormalizeHttpsUrl(ticket.PictureUrl, out var pictureUrl)
                    ? pictureUrl
                    : null
            };

            var createResult = await _userManager.CreateAsync(user);
            createResult.ThrowIfFailed();

            var roleResult = await _userManager.AddToRoleAsync(user, RoleNames.User);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                roleResult.ThrowIfFailed();
            }

            var loginResult = await _userManager.AddLoginAsync(
                user,
                new UserLoginInfo(ticket.Provider, ticket.ProviderKey, ticket.Provider));
            if (!loginResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                loginResult.ThrowIfFailed();
            }
        }

        await ValidateExternalUserCanAuthenticateAsync(user);
        ticket.UserId = user.Id;
        ticket.ConsumedAtUtc = DateTime.UtcNow;

        try
        {
            var session = await CreateSessionAsync(user, ipAddress, userAgent, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ExternalRegistrationServiceResultDTO
            {
                Session = session,
                ReturnPath = ticket.ReturnPath
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new UnauthorizedException("Google qeydiyyat kodu artıq istifadə olunub.");
        }
    }

    public async Task<string> CreateGoogleLinkIntentAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");
        await ValidateExternalUserCanAuthenticateAsync(user);
        var profile = new ExternalProviderProfileDTO
        {
            Provider = "Google",
            ProviderKey = user.Id.ToString(),
            Email = user.Email ?? string.Empty,
            EmailVerified = user.EmailConfirmed,
            DisplayName = user.UserName
        };
        return await CreateExternalAuthTicketAsync(
            ExternalAuthTicketKind.LinkIntent,
            profile,
            user.Id,
            "/settings?section=integrations",
            cancellationToken);
    }

    public async Task<Guid> ConsumeGoogleLinkIntentAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var ticket = await GetValidExternalTicketAsync(code, cancellationToken);
        if (ticket.Kind != ExternalAuthTicketKind.LinkIntent || !ticket.UserId.HasValue)
            throw new BadRequestException("Google bağlantı sorğusu etibarlı deyil.");
        ticket.ConsumedAtUtc = DateTime.UtcNow;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new UnauthorizedException("Google bağlantı sorğusu artıq istifadə olunub.");
        }
        return ticket.UserId.Value;
    }

    public async Task<ExternalLoginStatusDTO> GetExternalLoginStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");
        var logins = await _userManager.GetLoginsAsync(user);
        var googleLinked = logins.Any(login =>
            string.Equals(login.LoginProvider, "Google", StringComparison.Ordinal));
        var hasPassword = await _userManager.HasPasswordAsync(user);

        return new ExternalLoginStatusDTO
        {
            GoogleEnabled = _googleSettings.Enabled,
            GoogleLinked = googleLinked,
            HasPassword = hasPassword,
            CanUnlinkGoogle = googleLinked && (hasPassword || logins.Count > 1),
            GoogleEmail = googleLinked ? user.Email : null
        };
    }

    public async Task<ExternalLoginOperationResultDTO> LinkExternalLoginAsync(
        Guid userId,
        ExternalProviderProfileDTO profile,
        CancellationToken cancellationToken = default)
    {
        ValidateExternalProfile(profile);
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");
        await ValidateExternalUserCanAuthenticateAsync(user);

        var owner = await _userManager.FindByLoginAsync(profile.Provider, profile.ProviderKey);
        if (owner is not null && owner.Id != user.Id)
            throw new ConflictException("Bu Google hesabı artıq başqa WellPlayd hesabına bağlıdır.");

        var emailOwner = await _userManager.FindByEmailAsync(profile.Email);
        if (emailOwner is not null && emailOwner.Id != user.Id)
            throw new ConflictException("Bu Google emaili başqa WellPlayd hesabında istifadə olunur.");

        var logins = await _userManager.GetLoginsAsync(user);
        var currentGoogleLogin = logins.FirstOrDefault(login =>
            string.Equals(login.LoginProvider, profile.Provider, StringComparison.Ordinal));
        if (currentGoogleLogin is not null &&
            !string.Equals(currentGoogleLogin.ProviderKey, profile.ProviderKey, StringComparison.Ordinal))
        {
            throw new ConflictException(
                "Bu WellPlayd hesabına artıq başqa Google hesabı bağlıdır. Əvvəlcə onu ayırın.");
        }

        if (currentGoogleLogin is null)
        {
            var result = await _userManager.AddLoginAsync(
                user,
                new UserLoginInfo(profile.Provider, profile.ProviderKey, profile.Provider));
            if (!result.Succeeded)
                result.ThrowIfFailed();
        }

        return new ExternalLoginOperationResultDTO
        {
            Message = "Google hesabı uğurla bağlandı."
        };
    }

    public async Task<ExternalLoginOperationResultDTO> UnlinkGoogleAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");
        var logins = await _userManager.GetLoginsAsync(user);
        var googleLogin = logins.FirstOrDefault(login =>
            string.Equals(login.LoginProvider, "Google", StringComparison.Ordinal));
        if (googleLogin is null)
            return new ExternalLoginOperationResultDTO { Message = "Google hesabı bağlı deyil." };

        var hasPassword = await _userManager.HasPasswordAsync(user);
        if (!hasPassword && logins.Count <= 1)
        {
            throw new ConflictException(
                "Google bu hesabın yeganə giriş üsuludur. Əvvəlcə hesab üçün şifrə yaratmalısınız.");
        }

        var result = await _userManager.RemoveLoginAsync(
            user,
            googleLogin.LoginProvider,
            googleLogin.ProviderKey);
        result.ThrowIfFailed();

        return new ExternalLoginOperationResultDTO
        {
            Message = "Google hesabı WellPlayd hesabından ayrıldı."
        };
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

    private async Task<string> CreateExternalAuthTicketAsync(
        ExternalAuthTicketKind kind,
        ExternalProviderProfileDTO profile,
        Guid? userId,
        string returnPath,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expiredTickets = await _context.ExternalAuthTickets
            .Where(ticket => ticket.ExpiresAtUtc <= now ||
                             (ticket.ConsumedAtUtc != null && ticket.ConsumedAtUtc < now.AddHours(-1)))
            .ToListAsync(cancellationToken);
        if (expiredTickets.Count > 0)
            _context.ExternalAuthTickets.RemoveRange(expiredTickets);

        var rawCode = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        _context.ExternalAuthTickets.Add(new ExternalAuthTicket
        {
            Id = Guid.NewGuid(),
            CodeHash = HashToken(rawCode),
            Kind = kind,
            Provider = profile.Provider,
            ProviderKey = profile.ProviderKey,
            Email = profile.Email,
            EmailVerified = profile.EmailVerified,
            DisplayName = NormalizeLength(profile.DisplayName, 200),
            PictureUrl = TryNormalizeHttpsUrl(profile.PictureUrl, out var pictureUrl) ? pictureUrl : null,
            UserId = userId,
            ReturnPath = returnPath,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(_googleSettings.TicketLifetimeMinutes)
        });
        await _context.SaveChangesAsync(cancellationToken);
        return rawCode;
    }

    private async Task<ExternalAuthTicket> GetValidExternalTicketAsync(
        string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 256)
            throw new BadRequestException("Google giriş kodu etibarlı deyil.");

        var codeHash = HashToken(code.Trim());
        var ticket = await _context.ExternalAuthTickets
            .SingleOrDefaultAsync(item => item.CodeHash == codeHash, cancellationToken)
            ?? throw new UnauthorizedException("Google giriş kodu etibarlı deyil və ya vaxtı bitib.");

        if (ticket.ConsumedAtUtc.HasValue || ticket.ExpiresAtUtc <= DateTime.UtcNow)
            throw new UnauthorizedException("Google giriş kodu artıq istifadə olunub və ya vaxtı bitib.");

        return ticket;
    }

    private async Task<string> SuggestAvailableUserNameAsync(
        string? displayName,
        string email,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = string.IsNullOrWhiteSpace(displayName)
            ? email.Split('@')[0]
            : displayName;
        var normalized = new string(source
            .Where(character => char.IsLetterOrDigit(character) || character is '_' or '.')
            .ToArray());
        if (normalized.Length < 3)
            normalized = "player";
        if (normalized.Length > 24)
            normalized = normalized[..24];

        var candidate = normalized;
        for (var attempt = 0; attempt < 25; attempt++)
        {
            if (await _userManager.FindByNameAsync(candidate) is null)
                return candidate;
            candidate = $"{normalized}{RandomNumberGenerator.GetInt32(1000, 999999)}";
            if (candidate.Length > 30)
                candidate = candidate[..30];
        }

        return $"player{RandomNumberGenerator.GetInt32(100000, 999999)}";
    }

    private static void ValidateExternalProfile(ExternalProviderProfileDTO profile)
    {
        if (!string.Equals(profile.Provider, "Google", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(profile.ProviderKey))
            throw new BadRequestException("Google hesab məlumatları etibarlı deyil.");
        if (!profile.EmailVerified || string.IsNullOrWhiteSpace(profile.Email))
            throw new UnauthorizedException("Google email ünvanı təsdiqlənməyib.");
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(profile.Email))
            throw new BadRequestException("Google email ünvanı düzgün deyil.");
    }

    private static string NormalizeReturnPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith("/", StringComparison.Ordinal) ||
            value.StartsWith("//", StringComparison.Ordinal) ||
            value.Contains('\\') ||
            value.Length > 512)
            return "/";
        return value;
    }

    private static string? NormalizeLength(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maximumLength ? trimmed : trimmed[..maximumLength];
    }

    private static bool TryNormalizeHttpsUrl(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
            return false;
        normalized = uri.AbsoluteUri;
        return true;
    }

    private void ValidateSessionSettings()
    {
        if (_sessionSettings.RefreshTokenLifetimeDays is < 1 or > 365)
            throw new InvalidOperationException("AuthSession:RefreshTokenLifetimeDays 1-365 aralığında olmalıdır.");

        if (_sessionSettings.MaxActiveSessionsPerUser is < 1 or > 100)
            throw new InvalidOperationException("AuthSession:MaxActiveSessionsPerUser 1-100 aralığında olmalıdır.");
    }

    private async Task ValidateUserCanUseExternalLoginAsync(AppUser user)
    {
        if (await _userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedException(
                "Hesab müvəqqəti olaraq kilidlənib. Bir müddət sonra yenidən yoxlayın.");
        }

        if (user.AccountDeletionRequestedAtUtc.HasValue)
        {
            throw new UnauthorizedException(
                "Hesabın silinməsi başladılıb. Google ilə giriş mümkün deyil.");
        }

        EnsureUserIsNotBanned(user);
    }

    private async Task ValidateExternalUserCanAuthenticateAsync(AppUser user)
    {
        await ValidateUserCanUseExternalLoginAsync(user);
        ValidateUserCanAuthenticate(user);
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
