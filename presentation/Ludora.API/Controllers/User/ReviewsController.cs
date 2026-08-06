using System.Security.Claims;

using Ludora.API.Security;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Ludora.Domain.Enums;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers.User
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _reviewService;

        public ReviewsController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
        [HttpPost("{source}/{gameId:long}")]
        public async Task<ActionResult<ReviewDTO>> Create(
            CatalogSource source,
            long gameId,
            [FromBody] ReviewCreateDTO dto,
            CancellationToken cancellationToken)
        {
            var result = await _reviewService.CreateAsync(
                GetCurrentUserId(),
                source,
                gameId,
                dto,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { reviewId = result.Id },
                result);
        }

        [HttpGet("{reviewId:long}")]
        public async Task<ActionResult<ReviewDTO>> GetById(
            long reviewId,
            CancellationToken cancellationToken)
        {
            var result = await _reviewService.GetByIdAsync(
                reviewId,
                GetOptionalCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }

        [HttpGet("game/{source}/{gameId:long}")]
        public async Task<ActionResult<PagedResultDTO<ReviewDTO>>> GetGameReviews(
            CatalogSource source,
            long gameId,
            [FromQuery] ReviewFilterDTO filter,
            CancellationToken cancellationToken)
        {
            var result = await _reviewService.GetGameReviewsAsync(
                source,
                gameId,
                filter,
                GetOptionalCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }

        [HttpGet("user/{userId:guid}")]
        public async Task<ActionResult<PagedResultDTO<ReviewDTO>>> GetUserReviews(
            Guid userId,
            [FromQuery] ReviewFilterDTO filter,
            CancellationToken cancellationToken)
        {
            var result = await _reviewService.GetUserReviewsAsync(
                userId,
                filter,
                GetOptionalCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [HttpGet("mine")]
        public async Task<ActionResult<PagedResultDTO<ReviewDTO>>> GetMine(
            [FromQuery] ReviewFilterDTO filter,
            CancellationToken cancellationToken)
        {
            var result = await _reviewService.GetMineAsync(
                GetCurrentUserId(),
                filter,
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [HttpGet("mine/{source}/{gameId:long}")]
        public async Task<ActionResult<ReviewDTO>> GetMineForGame(
            CatalogSource source,
            long gameId,
            CancellationToken cancellationToken)
        {
            var result = await _reviewService.GetMineForGameAsync(
                GetCurrentUserId(),
                source,
                gameId,
                cancellationToken);

            return result is null
                ? NoContent()
                : Ok(result);
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
        [HttpPut("{reviewId:long}")]
        public async Task<ActionResult<ReviewDTO>> Update(
            long reviewId,
            [FromBody] ReviewUpdateDTO dto,
            CancellationToken cancellationToken)
        {
            var result = await _reviewService.UpdateAsync(
                GetCurrentUserId(),
                reviewId,
                dto,
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
        [HttpDelete("{reviewId:long}")]
        public async Task<IActionResult> Delete(
            long reviewId,
            CancellationToken cancellationToken)
        {
            await _reviewService.DeleteAsync(
                GetCurrentUserId(),
                reviewId,
                cancellationToken);

            return NoContent();
        }

        private Guid GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(value, out var userId))
            {
                throw new UnauthorizedAccessException(
                    "İstifadəçi müəyyən edilmədi.");
            }

            return userId;
        }

        private Guid? GetOptionalCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var userId)
                ? userId
                : null;
        }
    }
}
