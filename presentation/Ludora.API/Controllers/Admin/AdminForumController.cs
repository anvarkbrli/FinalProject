using Ludora.API.Extensions;
using Ludora.API.Security;
using Ludora.Application.Common;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers.Admin
{
    [Authorize(Policy = CapabilityNames.ViewForumModeration)]
    [EnableRateLimiting(RateLimitPolicyNames.Moderation)]
    [Route("api/admin/forum")]
    [ApiController]
    public sealed class AdminForumController : ControllerBase
    {
        private readonly IDiscussionService _discussionService;
        private readonly IDiscussionReplyService _replyService;

        public AdminForumController(
            IDiscussionService discussionService,
            IDiscussionReplyService replyService)
        {
            _discussionService = discussionService;
            _replyService = replyService;
        }

        [HttpGet("discussions/deleted")]
        public async Task<ActionResult<PagedResultDTO<DiscussionListItemDTO>>> GetDeletedDiscussions(
            [FromQuery] DiscussionFilterDTO filter,
            CancellationToken cancellationToken)
        {
            return Ok(await _discussionService.GetDeletedAsync(filter, cancellationToken));
        }

        [HttpDelete("discussions/{id:int}")]
        [Authorize(Policy = CapabilityNames.DeleteForumContent)]
        public async Task<IActionResult> DeleteDiscussion(
            int id,
            CancellationToken cancellationToken)
        {
            await _discussionService.DeleteAsync(
                id,
                User.GetRequiredUserId(),
                true,
                cancellationToken);

            return NoContent();
        }

        [HttpPost("discussions/{id:int}/pin")]
        [Authorize(Policy = CapabilityNames.ModerateForum)]
        public async Task<IActionResult> Pin(
            int id,
            CancellationToken cancellationToken)
        {
            await _discussionService.SetPinnedAsync(id, true, cancellationToken);
            return NoContent();
        }

        [HttpPost("discussions/{id:int}/unpin")]
        [Authorize(Policy = CapabilityNames.ModerateForum)]
        public async Task<IActionResult> Unpin(
            int id,
            CancellationToken cancellationToken)
        {
            await _discussionService.SetPinnedAsync(id, false, cancellationToken);
            return NoContent();
        }

        [HttpPost("discussions/{id:int}/lock")]
        [Authorize(Policy = CapabilityNames.ModerateForum)]
        public async Task<IActionResult> Lock(
            int id,
            CancellationToken cancellationToken)
        {
            await _discussionService.SetLockedAsync(id, true, cancellationToken);
            return NoContent();
        }

        [HttpPost("discussions/{id:int}/unlock")]
        [Authorize(Policy = CapabilityNames.ModerateForum)]
        public async Task<IActionResult> Unlock(
            int id,
            CancellationToken cancellationToken)
        {
            await _discussionService.SetLockedAsync(id, false, cancellationToken);
            return NoContent();
        }

        [HttpPost("discussions/{id:int}/restore")]
        [Authorize(Policy = CapabilityNames.RestoreForumContent)]
        public async Task<ActionResult<DiscussionDetailDTO>> RestoreDiscussion(
            int id,
            CancellationToken cancellationToken)
        {
            return Ok(await _discussionService.RestoreAsync(id, cancellationToken));
        }

        [HttpGet("replies/deleted")]
        public async Task<ActionResult<PagedResultDTO<DiscussionReplyDTO>>> GetDeletedReplies(
            [FromQuery] int? discussionId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var result = await _replyService.GetDeletedAsync(
                discussionId,
                pageNumber,
                pageSize,
                cancellationToken);

            return Ok(result);
        }

        [HttpDelete("replies/{replyId:int}")]
        [Authorize(Policy = CapabilityNames.DeleteForumContent)]
        public async Task<IActionResult> DeleteReply(
            int replyId,
            CancellationToken cancellationToken)
        {
            await _replyService.DeleteAsync(
                replyId,
                User.GetRequiredUserId(),
                true,
                cancellationToken);

            return NoContent();
        }

        [HttpPost("replies/{replyId:int}/restore")]
        [Authorize(Policy = CapabilityNames.RestoreForumContent)]
        public async Task<ActionResult<DiscussionReplyDTO>> RestoreReply(
            int replyId,
            CancellationToken cancellationToken)
        {
            return Ok(await _replyService.RestoreAsync(replyId, cancellationToken));
        }
    }
}
