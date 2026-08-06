using System.Security.Claims;

using Ludora.API.Security;
using Ludora.Application.DTOs.ExternalAuth;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Ludora.Domain.Entities.User;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Ludora.API.Controllers.User
{
    [ApiController]
    [Route("api/auth/google")]
    public sealed class GoogleAuthController : ControllerBase
    {
        private const string OperationKey = "wellplayd_google_operation";
        private const string ReturnPathKey = "wellplayd_return_path";
        private const string LinkUserIdKey = "wellplayd_link_user_id";

        private readonly IAuthService _authService;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly GoogleAuthenticationSettings _googleSettings;
        private readonly AuthSessionSettings _sessionSettings;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<GoogleAuthController> _logger;

        public GoogleAuthController(
            IAuthService authService,
            SignInManager<AppUser> signInManager,
            IOptions<GoogleAuthenticationSettings> googleOptions,
            IOptions<AuthSessionSettings> sessionOptions,
            IHostEnvironment environment,
            ILogger<GoogleAuthController> logger)
        {
            _authService = authService;
            _signInManager = signInManager;
            _googleSettings = googleOptions.Value;
            _sessionSettings = sessionOptions.Value;
            _environment = environment;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpGet("availability")]
        public IActionResult Availability() => Ok(new { enabled = _googleSettings.Enabled });

        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.Auth)]
        [HttpGet("start")]
        public IActionResult Start([FromQuery] string? returnPath = "/")
        {
            EnsureGoogleEnabled();
            var callbackUrl = Url.ActionLink(nameof(Callback), values: null, protocol: Request.Scheme)
                ?? throw new InvalidOperationException("Google callback URL yaradıla bilmədi.");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(
                GoogleDefaults.AuthenticationScheme,
                callbackUrl);
            properties.Items[OperationKey] = "login";
            properties.Items[ReturnPathKey] = NormalizeReturnPath(returnPath);
            properties.IsPersistent = false;
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.Auth)]
        [HttpPost("link-intent")]
        public async Task<IActionResult> CreateLinkIntent(CancellationToken cancellationToken)
        {
            EnsureGoogleEnabled();
            var code = await _authService.CreateGoogleLinkIntentAsync(GetCurrentUserId(), cancellationToken);
            var url = Url.ActionLink(nameof(Link), values: new { intent = code }, protocol: Request.Scheme)
                ?? throw new InvalidOperationException("Google link URL yaradıla bilmədi.");
            return Ok(new { url });
        }

        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.Auth)]
        [HttpGet("link")]
        public async Task<IActionResult> Link([FromQuery] string intent, CancellationToken cancellationToken)
        {
            EnsureGoogleEnabled();
            var userId = await _authService.ConsumeGoogleLinkIntentAsync(intent, cancellationToken);
            var callbackUrl = Url.ActionLink(nameof(Callback), values: null, protocol: Request.Scheme)
                ?? throw new InvalidOperationException("Google callback URL yaradıla bilmədi.");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(
                GoogleDefaults.AuthenticationScheme,
                callbackUrl,
                userId.ToString());
            properties.Items[OperationKey] = "link";
            properties.Items[LinkUserIdKey] = userId.ToString();
            properties.Items[ReturnPathKey] = "/settings?section=integrations";
            properties.IsPersistent = false;
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [AllowAnonymous]
        [HttpGet("callback")]
        public async Task<IActionResult> Callback(CancellationToken cancellationToken)
        {
            EnsureGoogleEnabled();
            var externalAuthentication = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
            if (!externalAuthentication.Succeeded || externalAuthentication.Principal is null)
                return RedirectToFrontendError("google_auth_failed");

            var operation = GetAuthenticationPropertyItem(
                externalAuthentication.Properties,
                OperationKey) ?? "login";
            var returnPath = NormalizeReturnPath(GetAuthenticationPropertyItem(
                externalAuthentication.Properties,
                ReturnPathKey));
            var expectedXsrf = operation == "link"
                ? GetAuthenticationPropertyItem(
                    externalAuthentication.Properties,
                    LinkUserIdKey)
                : null;

            try
            {
                var info = await _signInManager.GetExternalLoginInfoAsync(expectedXsrf);
                if (info is null || !string.Equals(
                        info.LoginProvider,
                        GoogleDefaults.AuthenticationScheme,
                        StringComparison.Ordinal))
                {
                    return RedirectToFrontendError("google_identity_missing");
                }

                var profile = BuildProfile(info);
                if (operation == "link")
                {
                    if (!Guid.TryParse(expectedXsrf, out var userId))
                        return RedirectToSettings("error", "google_link_invalid");

                    await _authService.LinkExternalLoginAsync(userId, profile, cancellationToken);
                    return RedirectToSettings("success", null);
                }

                var result = await _authService.PrepareExternalLoginAsync(
                    profile,
                    returnPath,
                    cancellationToken);
                var redirectUrl = QueryHelpers.AddQueryString(
                    _googleSettings.FrontendCallbackUrl,
                    new Dictionary<string, string?>
                    {
                        ["code"] = result.Code,
                        ["mode"] = result.RequiresProfileCompletion ? "complete" : "login"
                    });
                return Redirect(redirectUrl);
            }
            catch (Exception exception) when (exception is BadRequestException or UnauthorizedException or ConflictException or ForbiddenException)
            {
                _logger.LogWarning(exception, "Google authentication callback rejected.");
                return operation == "link"
                    ? RedirectToSettings("error", "google_link_rejected")
                    : RedirectToFrontendError("google_auth_rejected");
            }
            finally
            {
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            }
        }

        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.Auth)]
        [HttpPost("exchange")]
        public async Task<IActionResult> Exchange(
            [FromBody] ExternalAuthExchangeRequestDTO dto,
            CancellationToken cancellationToken)
        {
            EnsureGoogleEnabled();
            var result = await _authService.ExchangeExternalAuthCodeAsync(
                dto.Code,
                GetClientIpAddress(),
                Request.Headers.UserAgent.ToString(),
                cancellationToken);

            if (result.Session is not null)
                WriteRefreshCookie(result.Session.RefreshToken, result.Session.RefreshTokenExpiresAtUtc);

            return Ok(new ExternalAuthExchangeResultDTO
            {
                RequiresProfileCompletion = result.RequiresProfileCompletion,
                Session = result.Session?.Response,
                RegistrationToken = result.RegistrationToken,
                SuggestedUserName = result.SuggestedUserName,
                Email = result.Email,
                DisplayName = result.DisplayName,
                PictureUrl = result.PictureUrl,
                ReturnPath = result.ReturnPath
            });
        }

        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.Auth)]
        [HttpPost("complete-registration")]
        public async Task<IActionResult> CompleteRegistration(
            [FromBody] CompleteExternalRegistrationDTO dto,
            CancellationToken cancellationToken)
        {
            EnsureGoogleEnabled();
            var result = await _authService.CompleteExternalRegistrationAsync(
                dto,
                GetClientIpAddress(),
                Request.Headers.UserAgent.ToString(),
                cancellationToken);
            WriteRefreshCookie(result.Session.RefreshToken, result.Session.RefreshTokenExpiresAtUtc);
            return Ok(new ExternalAuthExchangeResultDTO
            {
                RequiresProfileCompletion = false,
                Session = result.Session.Response,
                ReturnPath = result.ReturnPath
            });
        }

        [Authorize]
        [HttpGet("status")]
        public async Task<IActionResult> Status(CancellationToken cancellationToken) =>
            Ok(await _authService.GetExternalLoginStatusAsync(GetCurrentUserId(), cancellationToken));

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.Auth)]
        [HttpDelete("link")]
        public async Task<IActionResult> Unlink(CancellationToken cancellationToken) =>
            Ok(await _authService.UnlinkGoogleAsync(GetCurrentUserId(), cancellationToken));

        private ExternalProviderProfileDTO BuildProfile(ExternalLoginInfo info)
        {
            var email = info.Principal.FindFirstValue(ClaimTypes.Email)?.Trim() ?? string.Empty;
            var verifiedClaim = info.Principal.FindFirstValue("google_email_verified");
            var emailVerified = string.Equals(verifiedClaim, "true", StringComparison.OrdinalIgnoreCase);
            return new ExternalProviderProfileDTO
            {
                Provider = GoogleDefaults.AuthenticationScheme,
                ProviderKey = info.ProviderKey,
                Email = email,
                EmailVerified = emailVerified,
                DisplayName = info.Principal.FindFirstValue(ClaimTypes.Name),
                PictureUrl = info.Principal.FindFirstValue("google_picture")
            };
        }

        private IActionResult RedirectToFrontendError(string error)
        {
            return Redirect(QueryHelpers.AddQueryString(
                _googleSettings.FrontendCallbackUrl,
                "error",
                error));
        }

        private IActionResult RedirectToSettings(string status, string? error)
        {
            var baseUrl = new Uri(_googleSettings.FrontendCallbackUrl).GetLeftPart(UriPartial.Authority);
            var parameters = new List<KeyValuePair<string, string?>>
            {
                new("section", "integrations"),
                new("googleLink", status)
            };
            if (!string.IsNullOrWhiteSpace(error))
                parameters.Add(new("googleError", error));
            return Redirect(QueryHelpers.AddQueryString(
                baseUrl + "/settings",
                parameters.ToDictionary(item => item.Key, item => item.Value)));
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
                    Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)),
                    IsEssential = true
                });
        }

        private void EnsureGoogleEnabled()
        {
            if (!_googleSettings.Enabled)
                throw new NotFoundException("Google ilə giriş aktiv deyil.");
        }

        private Guid GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId)
                ? userId
                : throw new UnauthorizedException("İstifadəçi identifikatoru tapılmadı.");
        }

        private string? GetClientIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

        private static string? GetAuthenticationPropertyItem(
            AuthenticationProperties? properties,
            string key)
        {
            return properties is not null &&
                   properties.Items.TryGetValue(key, out var value)
                ? value
                : null;
        }

        private static string NormalizeReturnPath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith('/') ||
                value.StartsWith("//", StringComparison.Ordinal) || value.Contains('\\') || value.Length > 512)
                return "/";
            return value;
        }
    }
}
