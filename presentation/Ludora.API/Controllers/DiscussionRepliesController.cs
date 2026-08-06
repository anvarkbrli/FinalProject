using System.Security.Claims;

using Ludora.API.Security;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers
{
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
                CanManageContent(),
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
                CanManageContent(),
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
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
        [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
        [HttpPut("discussion-replies/{replyId:int}")]
        public async Task<ActionResult<DiscussionReplyDTO>> Update(
            int replyId,
            [FromBody] DiscussionReplyUpdateDTO dto,
            CancellationToken cancellationToken)
        {
            var result = await _replyService.UpdateAsync(
                replyId,
                GetCurrentUserId(),
                CanManageContent(),
                dto,
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
        [HttpDelete("discussion-replies/{replyId:int}")]
        public async Task<IActionResult> Delete(
            int replyId,
            CancellationToken cancellationToken)
        {
            await _replyService.DeleteAsync(
                replyId,
                GetCurrentUserId(),
                CanManageContent(),
                cancellationToken);

            return NoContent();
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
        [HttpPost("discussion-replies/{replyId:int}/like")]
        public async Task<IActionResult> Like(
            int replyId,
            CancellationToken cancellationToken)
        {
            await _replyService.LikeAsync(replyId, GetCurrentUserId(), cancellationToken);
            return NoContent();
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
        [HttpDelete("discussion-replies/{replyId:int}/like")]
        public async Task<IActionResult> Unlike(
            int replyId,
            CancellationToken cancellationToken)
        {
            await _replyService.UnlikeAsync(replyId, GetCurrentUserId(), cancellationToken);
            return NoContent();
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
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
                CanManageContent(),
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
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
        [EnableRateLimiting(RateLimitPolicyNames.ForumWrite)]
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

        private bool CanManageContent() =>
            User.CanDeleteForumContent();

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
}
