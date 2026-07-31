using Ludora.API.Security;
using System.Security.Claims;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers.User;

[Route("api/posts")]
[ApiController]
public sealed class PostInteractionsController : ControllerBase
{
    private readonly IPostInteractionService _service;

    public PostInteractionsController(IPostInteractionService service) => _service = service;

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
    [HttpPost("{postId:long}/likes")]
    public async Task<IActionResult> Like(long postId, CancellationToken cancellationToken)
    {
        await _service.LikeAsync(postId, GetCurrentUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
    [HttpDelete("{postId:long}/likes")]
    public async Task<IActionResult> Unlike(long postId, CancellationToken cancellationToken)
    {
        await _service.UnlikeAsync(postId, GetCurrentUserId(), cancellationToken);
        return NoContent();
    }

    [HttpGet("{postId:long}/likes")]
    public async Task<ActionResult<PagedResultDTO<InteractionLikeUserDTO>>> GetLikes(
        long postId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetLikesAsync(
            postId,
            pageNumber,
            pageSize,
            GetOptionalCurrentUserId(),
            cancellationToken));
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
    [HttpPost("{postId:long}/comments")]
    public async Task<ActionResult<PostCommentDTO>> CreateComment(
        long postId,
        InteractionCommentCreateDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateCommentAsync(postId, GetCurrentUserId(), dto, cancellationToken);
        return Created($"api/posts/{postId}/comments/{result.Id}", result);
    }

    [HttpGet("{postId:long}/comments")]
    public async Task<ActionResult<PagedResultDTO<PostCommentDTO>>> GetComments(
        long postId,
        [FromQuery] InteractionCommentFilterDTO filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetCommentsAsync(
            postId,
            filter,
            GetOptionalCurrentUserId(),
            cancellationToken));
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
    [HttpPut("comments/{commentId:long}")]
    public async Task<ActionResult<PostCommentDTO>> UpdateComment(
        long commentId,
        InteractionCommentUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.UpdateCommentAsync(commentId, GetCurrentUserId(), dto, cancellationToken));
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
    [HttpDelete("comments/{commentId:long}")]
    public async Task<IActionResult> DeleteComment(long commentId, CancellationToken cancellationToken)
    {
        await _service.DeleteCommentAsync(commentId, GetCurrentUserId(), cancellationToken);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("İstifadəçi müəyyən edilmədi.");
        return userId;
    }

    private Guid? GetOptionalCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
