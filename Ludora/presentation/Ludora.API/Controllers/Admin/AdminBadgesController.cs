using Ludora.Application.Common;
using Ludora.Application.DTOs.Badge;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers.Admin;

[ApiController]
[Authorize(Roles = RoleNames.AdminOrModerator)]
[Route("api/admin/badges")]
public sealed class AdminBadgesController : ControllerBase
{
    private readonly IBadgeService _badgeService;

    public AdminBadgesController(IBadgeService badgeService)
    {
        _badgeService = badgeService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BadgeDTO>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await _badgeService.GetAllAsync(true, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<BadgeDTO>> Create(
        [FromBody] BadgeUpsertDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await _badgeService.CreateAsync(dto, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<BadgeDTO>> Update(
        int id,
        [FromBody] BadgeUpsertDTO dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _badgeService.UpdateAsync(id, dto, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        await _badgeService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("evaluate/users/{userId:guid}")]
    public async Task<ActionResult<object>> EvaluateUser(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var awardedCount = await _badgeService.EvaluateUserAsync(
            userId,
            cancellationToken);
        return Ok(new { awardedCount });
    }
}
