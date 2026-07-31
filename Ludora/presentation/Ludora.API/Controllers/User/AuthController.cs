using Ludora.API.Security;
using System.Security.Claims;
using Ludora.Application.DTOs.JWT;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Ludora.API.Controllers.User;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IEmailService _emailService;
    private readonly EmailSettings _emailSettings;
    private readonly AuthSessionSettings _sessionSettings;
    private readonly IHostEnvironment _environment;

    public AuthController(
        IAuthService authService,
        IEmailService emailService,
        IOptions<EmailSettings> emailOptions,
        IOptions<AuthSessionSettings> sessionOptions,
        IHostEnvironment environment)
    {
        _authService = authService;
        _emailService = emailService;
        _emailSettings = emailOptions.Value;
        _sessionSettings = sessionOptions.Value;
        _environment = environment;
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(dto, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginDTO dto,
        CancellationToken cancellationToken)
    {
        var session = await _authService.LoginAsync(
            dto,
            GetClientIpAddress(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        WriteRefreshCookie(
            session.RefreshToken,
            session.RefreshTokenExpiresAtUtc);

        return Ok(session.Response);
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        EnsureCookieRequestIsProtected();
        var refreshToken = Request.Cookies[_sessionSettings.RefreshCookieName];

        try
        {
            var session = await _authService.RefreshAsync(
                refreshToken ?? string.Empty,
                GetClientIpAddress(),
                Request.Headers.UserAgent.ToString(),
                cancellationToken);

            WriteRefreshCookie(
                session.RefreshToken,
                session.RefreshTokenExpiresAtUtc);

            return Ok(session.Response);
        }
        catch (UnauthorizedException)
        {
            DeleteRefreshCookie();
            throw;
        }
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        EnsureCookieRequestIsProtected();
        var refreshToken = Request.Cookies[_sessionSettings.RefreshCookieName];

        await _authService.LogoutAsync(
            refreshToken,
            GetClientIpAddress(),
            cancellationToken);

        DeleteRefreshCookie();
        return NoContent();
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(
        [FromQuery] Guid userId,
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        var result = await _authService.ConfirmEmailAsync(
            userId,
            token,
            cancellationToken);

        return Ok(result);
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] ConfirmEmailDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await _authService.ConfirmEmailAsync(
            dto.UserId,
            dto.Token,
            cancellationToken);

        return Ok(result);
    }

    [EnableRateLimiting(RateLimitPolicyNames.EmailAuth)]
    [HttpPost("resend-email-confirmation")]
    public async Task<IActionResult> ResendEmailConfirmation(
        [FromBody] ResendEmailConfirmationDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await _authService.ResendEmailConfirmationAsync(
            dto,
            cancellationToken);

        return Ok(result);
    }

    [EnableRateLimiting(RateLimitPolicyNames.EmailAuth)]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordDTO dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _authService.ForgotPasswordAsync(dto, cancellationToken));
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordDTO dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _authService.ResetPasswordAsync(dto, cancellationToken));
    }

    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[_sessionSettings.RefreshCookieName];
        var sessions = await _authService.GetActiveSessionsAsync(
            GetCurrentUserId(),
            refreshToken,
            cancellationToken);
        return Ok(sessions);
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[_sessionSettings.RefreshCookieName];
        var result = await _authService.RevokeSessionAsync(
            GetCurrentUserId(),
            sessionId,
            refreshToken,
            GetClientIpAddress(),
            cancellationToken);

        if (result.CurrentSessionRevoked)
            DeleteRefreshCookie();

        return Ok(result);
    }

    [ApiExplorerSettings(IgnoreApi = false)]
    [HttpGet("email-diagnostics")]
    public IActionResult GetEmailDiagnostics()
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        return Ok(new EmailDiagnosticsDTO
        {
            Enabled = _emailSettings.Enabled,
            Environment = _environment.EnvironmentName,
            Host = _emailSettings.Host,
            Port = _emailSettings.Port,
            EnableSsl = _emailSettings.EnableSsl,
            UserNameConfigured = !string.IsNullOrWhiteSpace(_emailSettings.UserName),
            PasswordConfigured = !string.IsNullOrWhiteSpace(_emailSettings.Password),
            FromEmail = _emailSettings.FromEmail,
            PublicApiBaseUrl = _emailSettings.PublicApiBaseUrl,
            PublicFrontendBaseUrl = _emailSettings.PublicFrontendBaseUrl,
            MaxRetryAttempts = _emailSettings.MaxRetryAttempts,
            SendTimeoutSeconds = _emailSettings.SendTimeoutSeconds
        });
    }

    [EnableRateLimiting(RateLimitPolicyNames.Auth)]
    [HttpPost("email-diagnostics/test")]
    public async Task<IActionResult> SendEmailDiagnosticsTest(
        [FromBody] EmailDeliveryTestDTO dto,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        var testLink = _emailSettings.PublicFrontendBaseUrl.TrimEnd('/') + "/";

        await _emailService.SendEmailConfirmationAsync(
            dto.Email.Trim(),
            "SMTP test istifadəçisi",
            testLink,
            cancellationToken);

        return Ok(new
        {
            message = "Test emaili göndəriş növbəsinə əlavə edildi. Outbox worker və SMTP loglarını yoxlayın."
        });
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("İstifadəçi identifikatoru tapılmadı.");
    }

    private string? GetClientIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString();

    private void EnsureCookieRequestIsProtected()
    {
        const string headerName = "X-WellPlayd-CSRF";
        if (!string.Equals(
                Request.Headers[headerName].ToString(),
                "1",
                StringComparison.Ordinal))
        {
            throw new BadRequestException("Sessiya sorğusunun təhlükəsizlik başlığı tapılmadı.");
        }
    }

    private void WriteRefreshCookie(string token, DateTime expiresAtUtc)
    {
        var isHttps = !_environment.IsDevelopment() || Request.IsHttps;

        Response.Cookies.Append(
            _sessionSettings.RefreshCookieName,
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = isHttps,
                SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
                Path = "/api/auth",
                Expires = new DateTimeOffset(
                    DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)),
                IsEssential = true
            });
    }

    private void DeleteRefreshCookie()
    {
        var isHttps = !_environment.IsDevelopment() || Request.IsHttps;

        Response.Cookies.Delete(
            _sessionSettings.RefreshCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = isHttps,
                SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
                Path = "/api/auth",
                IsEssential = true
            });
    }
}
