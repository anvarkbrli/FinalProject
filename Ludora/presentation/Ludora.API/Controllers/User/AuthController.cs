using Ludora.Application.DTOs.JWT;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers.User;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
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
}
