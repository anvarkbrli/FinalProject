using Ludora.Application.DTOs.JWT;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
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
    private readonly IHostEnvironment _environment;

    public AuthController(
        IAuthService authService,
        IEmailService emailService,
        IOptions<EmailSettings> emailOptions,
        IHostEnvironment environment)
    {
        _authService = authService;
        _emailService = emailService;
        _emailSettings = emailOptions.Value;
        _environment = environment;
    }

    [EnableRateLimiting("Auth")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(dto, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [EnableRateLimiting("Auth")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(dto, cancellationToken);

        return Ok(result);
    }

    [EnableRateLimiting("Auth")]
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

    [EnableRateLimiting("Auth")]
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

    [EnableRateLimiting("Auth")]
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

    // Yalnız development mühitində SMTP konfiqurasiyasının həqiqətən
    // yüklənib-yüklənmədiyini secret-ləri göstərmədən yoxlamaq üçündür.
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
            UserNameConfigured =
                !string.IsNullOrWhiteSpace(_emailSettings.UserName),
            PasswordConfigured =
                !string.IsNullOrWhiteSpace(_emailSettings.Password),
            FromEmail = _emailSettings.FromEmail,
            PublicApiBaseUrl = _emailSettings.PublicApiBaseUrl,
            PublicFrontendBaseUrl = _emailSettings.PublicFrontendBaseUrl,
            MaxRetryAttempts = _emailSettings.MaxRetryAttempts,
            SendTimeoutSeconds = _emailSettings.SendTimeoutSeconds
        });
    }

    // Swagger-dən real SMTP göndərişini ayrıca sınaqdan keçirməyə imkan verir.
    // Production mühitində endpoint mövcud deyil.
    [EnableRateLimiting("Auth")]
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
            message =
                "SMTP server məktubu qəbul etdi. Inbox, Spam/Junk və server loglarını yoxlayın."
        });
    }
}
