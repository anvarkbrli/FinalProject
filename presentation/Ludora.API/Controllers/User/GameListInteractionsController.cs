using Ludora.API.Security;
using System.Security.Claims;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers.User;

[Route("api/game-lists")]
[ApiController]
public sealed class GameListInteractionsController : ControllerBase
{
    private readonly IGameListInteractionService _service;

    public GameListInteractionsController(IGameListInteractionService service) => _service = service;

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
    [HttpPost("{gameListId:int}/likes")]
    public async Task<IActionResult> Like(int gameListId, CancellationToken cancellationToken)
    {
        await _service.LikeAsync(gameListId, GetCurrentUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
    [HttpDelete("{gameListId:int}/likes")]
    public async Task<IActionResult> Unlike(int gameListId, CancellationToken cancellationToken)
    {
        await _service.UnlikeAsync(gameListId, GetCurrentUserId(), cancellationToken);
        return NoContent();
    }

    [HttpGet("{gameListId:int}/likes")]
    public async Task<ActionResult<PagedResultDTO<InteractionLikeUserDTO>>> GetLikes(
        int gameListId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetLikesAsync(
            gameListId,
            pageNumber,
            pageSize,
            GetOptionalCurrentUserId(),
            cancellationToken));
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
    [HttpPost("{gameListId:int}/comments")]
    public async Task<ActionResult<GameListCommentDTO>> CreateComment(
        int gameListId,
        InteractionCommentCreateDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateCommentAsync(gameListId, GetCurrentUserId(), dto, cancellationToken);
        return Created($"api/game-lists/{gameListId}/comments/{result.Id}", result);
    }

    [HttpGet("{gameListId:int}/comments")]
    public async Task<ActionResult<PagedResultDTO<GameListCommentDTO>>> GetComments(
        int gameListId,
        [FromQuery] InteractionCommentFilterDTO filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetCommentsAsync(
            gameListId,
            filter,
            GetOptionalCurrentUserId(),
            cancellationToken));
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
    [HttpPut("comments/{commentId:long}")]
    public async Task<ActionResult<GameListCommentDTO>> UpdateComment(
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
