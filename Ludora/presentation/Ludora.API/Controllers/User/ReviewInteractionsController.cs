using System.Security.Claims;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers.User;

[Route("api/reviews")]
[ApiController]
public class ReviewInteractionsController : ControllerBase
{
    private readonly IReviewInteractionService _service;
    public ReviewInteractionsController(IReviewInteractionService service) => _service = service;

    [Authorize, HttpPost("{reviewId:long}/likes")]
    public async Task<IActionResult> Like(long reviewId, CancellationToken ct)
    { await _service.LikeAsync(reviewId, CurrentUserId(), ct); return NoContent(); }

    [Authorize, HttpDelete("{reviewId:long}/likes")]
    public async Task<IActionResult> Unlike(long reviewId, CancellationToken ct)
    { await _service.UnlikeAsync(reviewId, CurrentUserId(), ct); return NoContent(); }

    [HttpGet("{reviewId:long}/likes")]
    public async Task<ActionResult<PagedResultDTO<ReviewLikeUserDTO>>> GetLikes(
        long reviewId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _service.GetLikesAsync(
            reviewId,
            pageNumber,
            pageSize,
            OptionalUserId(),
            ct));

    [Authorize, HttpPost("{reviewId:long}/comments")]
    public async Task<ActionResult<ReviewCommentDTO>> CreateComment(long reviewId, ReviewCommentCreateDTO dto, CancellationToken ct)
    {
        var result = await _service.CreateCommentAsync(reviewId, CurrentUserId(), dto, ct);
        return Created($"api/reviews/{reviewId}/comments/{result.Id}", result);
    }

    [HttpGet("{reviewId:long}/comments")]
    public async Task<ActionResult<PagedResultDTO<ReviewCommentDTO>>> GetComments(long reviewId, [FromQuery] ReviewCommentFilterDTO filter, CancellationToken ct)
        => Ok(await _service.GetCommentsAsync(reviewId, filter, OptionalUserId(), ct));

    [Authorize, HttpPut("comments/{commentId:long}")]
    public async Task<ActionResult<ReviewCommentDTO>> UpdateComment(long commentId, ReviewCommentUpdateDTO dto, CancellationToken ct)
        => Ok(await _service.UpdateCommentAsync(commentId, CurrentUserId(), dto, ct));

    [Authorize, HttpDelete("comments/{commentId:long}")]
    public async Task<IActionResult> DeleteComment(long commentId, CancellationToken ct)
    { await _service.DeleteCommentAsync(commentId, CurrentUserId(), ct); return NoContent(); }

    private Guid CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var id)) throw new UnauthorizedAccessException("İstifadəçi müəyyən edilmədi.");
        return id;
    }

    private Guid? OptionalUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
