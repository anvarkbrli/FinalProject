using System.Security.Claims;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers;

[Route("api")]
[ApiController]
public sealed class DiscussionRepliesController : ControllerBase
{
    private readonly IDiscussionReplyService _replyService;

    public DiscussionRepliesController(IDiscussionReplyService replyService)
    {
        _replyService = replyService;
    }

    [HttpGet("discussion-replies/{replyId:int}")]
    public async Task<ActionResult<DiscussionReplyDTO>> GetById(
        int replyId,
        CancellationToken cancellationToken)
    {
        var result = await _replyService.GetByIdAsync(
            replyId,
            GetOptionalCurrentUserId(),
            CanModerate(),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("discussions/{discussionId:int}/replies")]
    public async Task<ActionResult<PagedResultDTO<DiscussionReplyDTO>>> GetByDiscussion(
        int discussionId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _replyService.GetByDiscussionAsync(
            discussionId,
            pageNumber,
            pageSize,
            GetOptionalCurrentUserId(),
            CanModerate(),
            cancellationToken);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("discussions/{discussionId:int}/replies")]
    public async Task<ActionResult<DiscussionReplyDTO>> Create(
        int discussionId,
        [FromBody] DiscussionReplyCreateDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await _replyService.CreateAsync(
            discussionId,
            GetCurrentUserId(),
            dto,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { replyId = result.Id },
            result);
    }

    [Authorize]
    [HttpPut("discussion-replies/{replyId:int}")]
    public async Task<ActionResult<DiscussionReplyDTO>> Update(
        int replyId,
        [FromBody] DiscussionReplyUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await _replyService.UpdateAsync(
            replyId,
            GetCurrentUserId(),
            CanModerate(),
            dto,
            cancellationToken);

        return Ok(result);
    }

    [Authorize]
    [HttpDelete("discussion-replies/{replyId:int}")]
    public async Task<IActionResult> Delete(
        int replyId,
        CancellationToken cancellationToken)
    {
        await _replyService.DeleteAsync(
            replyId,
            GetCurrentUserId(),
            CanModerate(),
            cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpPost("discussion-replies/{replyId:int}/like")]
    public async Task<IActionResult> Like(
        int replyId,
        CancellationToken cancellationToken)
    {
        await _replyService.LikeAsync(replyId, GetCurrentUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("discussion-replies/{replyId:int}/like")]
    public async Task<IActionResult> Unlike(
        int replyId,
        CancellationToken cancellationToken)
    {
        await _replyService.UnlikeAsync(replyId, GetCurrentUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPut("discussion-replies/{replyId:int}/vote")]
    public async Task<ActionResult<DiscussionReplyDTO>> Vote(
        int replyId,
        [FromBody] DiscussionReplyVoteRequestDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await _replyService.SetVoteAsync(
            replyId,
            GetCurrentUserId(),
            dto.Value,
            CanModerate(),
            cancellationToken);

        return Ok(result);
    }

    [Authorize]
    [HttpPut("discussion-replies/{replyId:int}/follow")]
    public async Task<IActionResult> Follow(
        int replyId,
        CancellationToken cancellationToken)
    {
        await _replyService.FollowAsync(
            replyId,
            GetCurrentUserId(),
            cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpDelete("discussion-replies/{replyId:int}/follow")]
    public async Task<IActionResult> Unfollow(
        int replyId,
        CancellationToken cancellationToken)
    {
        await _replyService.UnfollowAsync(
            replyId,
            GetCurrentUserId(),
            cancellationToken);

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
