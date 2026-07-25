using System.Security.Claims;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers;

[Route("api/forum-feed")]
[ApiController]
public sealed class ForumFeedController : ControllerBase
{
    private readonly IForumFeedService _forumFeedService;

    public ForumFeedController(IForumFeedService forumFeedService)
    {
        _forumFeedService = forumFeedService;
    }

    [Authorize]
    [HttpGet("home")]
    public async Task<ActionResult<ForumFeedResultDTO>> GetHome(
        [FromQuery] ForumFeedFilterDTO filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _forumFeedService.GetHomeAsync(
            GetCurrentUserId(),
            filter,
            CanModerate(),
            cancellationToken));
    }

    [HttpGet("popular")]
    public async Task<ActionResult<ForumFeedResultDTO>> GetPopular(
        [FromQuery] ForumFeedFilterDTO filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _forumFeedService.GetPopularAsync(
            GetOptionalCurrentUserId(),
            filter,
            CanModerate(),
            cancellationToken));
    }

    [HttpGet("explore")]
    public async Task<ActionResult<ForumFeedResultDTO>> GetExplore(
        [FromQuery] ForumFeedFilterDTO filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _forumFeedService.GetExploreAsync(
            GetOptionalCurrentUserId(),
            filter,
            CanModerate(),
            cancellationToken));
    }

    private bool CanModerate() => User.IsInRole("Admin");

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
