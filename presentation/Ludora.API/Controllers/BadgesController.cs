using Ludora.Application.DTOs.Badge;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers;

[ApiController]
[Route("api/badges")]
public sealed class BadgesController : ControllerBase
{
    private readonly IBadgeService _badgeService;

    public BadgesController(IBadgeService badgeService)
    {
        _badgeService = badgeService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BadgeDTO>>> GetActive(
        CancellationToken cancellationToken)
    {
        return Ok(await _badgeService.GetAllAsync(false, cancellationToken));
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult<IReadOnlyList<UserBadgeDTO>>> GetUserBadges(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return Ok(await _badgeService.GetUserBadgesAsync(userId, cancellationToken));
    }
}
