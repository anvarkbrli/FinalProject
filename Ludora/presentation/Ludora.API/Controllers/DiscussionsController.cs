using System.Security.Claims;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers;

[Route("api/discussions")]
[ApiController]
public sealed class DiscussionsController : ControllerBase
{
    private readonly IDiscussionService _discussionService;

    public DiscussionsController(IDiscussionService discussionService)
    {
        _discussionService = discussionService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDTO<DiscussionListItemDTO>>> GetAll(
        [FromQuery] DiscussionFilterDTO filter,
        CancellationToken cancellationToken)
    {
        var result = await _discussionService.GetAllAsync(
            filter,
            GetOptionalCurrentUserId(),
            CanModerate(),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DiscussionDetailDTO>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _discussionService.GetByIdAsync(
            id,
            GetOptionalCurrentUserId(),
            CanModerate(),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("slug/{slug}")]
    public async Task<ActionResult<DiscussionDetailDTO>> GetBySlug(
        string slug,
        CancellationToken cancellationToken)
    {
        var result = await _discussionService.GetBySlugAsync(
            slug,
            GetOptionalCurrentUserId(),
            CanModerate(),
            cancellationToken);

        return Ok(result);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<DiscussionDetailDTO>> Create(
        [FromBody] DiscussionCreateDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await _discussionService.CreateAsync(
            GetCurrentUserId(),
            dto,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
    }

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<DiscussionDetailDTO>> Update(
        int id,
        [FromBody] DiscussionUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await _discussionService.UpdateAsync(
            id,
            GetCurrentUserId(),
            CanModerate(),
            dto,
            cancellationToken);

        return Ok(result);
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        await _discussionService.DeleteAsync(
            id,
            GetCurrentUserId(),
            CanModerate(),
            cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:int}/like")]
    public async Task<IActionResult> Like(
        int id,
        CancellationToken cancellationToken)
    {
        await _discussionService.LikeAsync(id, GetCurrentUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id:int}/like")]
    public async Task<IActionResult> Unlike(
        int id,
        CancellationToken cancellationToken)
    {
        await _discussionService.UnlikeAsync(id, GetCurrentUserId(), cancellationToken);
        return NoContent();
    }

    private bool CanModerate()
    {
        return User.IsInRole("Admin");
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
            throw new UnauthorizedAccessException("İstifadəçi müəyyən edilmədi.");

        return userId;
    }

    private Guid? GetOptionalCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}
