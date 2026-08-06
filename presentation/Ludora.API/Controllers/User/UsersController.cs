using System.Security.Claims;

using Ludora.API.Models;
using Ludora.API.Security;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers.User
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IFollowService _followService;
        private readonly IBlockService _blockService;
        private readonly IUserActivityService _userActivityService;
        private readonly IAccountDeletionService _accountDeletionService;

        public UsersController(
            IUserService userService,
            IFollowService followService,
            IBlockService blockService,
            IUserActivityService userActivityService,
            IAccountDeletionService accountDeletionService)
        {
            _userService = userService;
            _followService = followService;
            _blockService = blockService;
            _userActivityService = userActivityService;
            _accountDeletionService = accountDeletionService;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var result = await _userService.GetCurrentUserAsync(
                GetCurrentUserId());

            return Ok(result);
        }

        [Authorize]
        [HttpPut("me/language")]
        public async Task<IActionResult> UpdatePreferredLanguage(
            [FromBody] UpdatePreferredLanguageDTO dto,
            CancellationToken cancellationToken)
        {
            var result = await _userService.UpdatePreferredLanguageAsync(
                GetCurrentUserId(),
                dto,
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [HttpGet("me/feed")]
        public async Task<IActionResult> GetMyFeed(
            [FromQuery] UserActivityFilterDTO filter,
            CancellationToken cancellationToken)
        {
            var result = await _userActivityService.GetFeedAsync(
                GetCurrentUserId(),
                filter,
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [HttpGet("me/activities")]
        [Obsolete("Use GET api/users/me/feed instead.")]
        public async Task<IActionResult> GetMyActivities(
            [FromQuery] UserActivityFilterDTO filter,
            CancellationToken cancellationToken)
        {
            var result = await _userActivityService.GetFeedAsync(
                GetCurrentUserId(),
                filter,
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [HttpGet("me/settings")]
        public async Task<IActionResult> GetSettings()
        {
            var result = await _userService.GetSettingsAsync(
                GetCurrentUserId());

            return Ok(result);
        }

        [Authorize]
        [HttpPatch("me/settings")]
        public async Task<IActionResult> UpdateSettings(
            [FromBody] UpdateUserSettingsDTO dto)
        {
            var result = await _userService.UpdateSettingsAsync(
                GetCurrentUserId(),
                dto);

            return Ok(result);
        }

        [Authorize]
        [HttpGet("me/forum-interests")]
        public async Task<IActionResult> GetForumInterests(
            CancellationToken cancellationToken)
        {
            var result = await _userService.GetForumInterestsAsync(
                GetCurrentUserId(),
                cancellationToken);

            return result is null
                ? new JsonResult(null) { StatusCode = StatusCodes.Status200OK }
                : Ok(result);
        }

        [Authorize]
        [HttpPut("me/forum-interests")]
        public async Task<IActionResult> UpdateForumInterests(
            [FromBody] UpdateForumInterestProfileDTO dto,
            CancellationToken cancellationToken)
        {
            var result = await _userService.UpdateForumInterestsAsync(
                GetCurrentUserId(),
                dto,
                cancellationToken);

            return Ok(result);
        }

        [HttpGet("{userName}")]
        public async Task<IActionResult> GetByUserName(string userName)
        {
            var result = await _userService.GetByUserNameAsync(
                userName,
                GetOptionalCurrentUserId());

            return Ok(result);
        }

        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateProfile(
            [FromBody] UpdateProfileDTO dto)
        {
            var result = await _userService.UpdateProfileAsync(
                GetCurrentUserId(),
                dto);

            return Ok(result);
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.AccountSensitive)]
        [HttpPost("me/password-change")]
        public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordDTO dto)
        {
            var result = await _userService.ChangePasswordAsync(
                GetCurrentUserId(),
                dto);

            return Ok(result);
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.AccountSensitive)]
        [HttpPost("me/email-change")]
        public async Task<IActionResult> RequestEmailChange(
            [FromBody] ChangeEmailRequestDTO dto,
            CancellationToken cancellationToken)
        {
            var result = await _userService.RequestEmailChangeAsync(
                GetCurrentUserId(),
                dto,
                cancellationToken);

            return Ok(result);
        }

        [EnableRateLimiting(RateLimitPolicyNames.AccountSensitive)]
        [HttpGet("email-change/confirm")]
        public async Task<IActionResult> ConfirmEmailChange(
            [FromQuery] Guid userId,
            [FromQuery] string newEmail,
            [FromQuery] string token,
            CancellationToken cancellationToken)
        {
            var result = await _userService.ConfirmEmailChangeAsync(
                userId,
                newEmail,
                token,
                cancellationToken);

            return Ok(result);
        }

        [EnableRateLimiting(RateLimitPolicyNames.AccountSensitive)]
        [HttpPost("email-change/confirm")]
        public async Task<IActionResult> ConfirmEmailChange(
            [FromBody] ConfirmEmailChangeDTO dto,
            CancellationToken cancellationToken)
        {
            var result = await _userService.ConfirmEmailChangeAsync(
                dto.UserId,
                dto.NewEmail,
                dto.Token,
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.Uploads)]
        [HttpPut("me/profile-image")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateProfileImage(
            [FromForm] SingleImageFormRequest request)
        {
            var image = request.Image;
            if (image is null || image.Length <= 0)
                return BadRequest(new { message = "Profil şəkli seçilməyib." });

            await using var stream = image.OpenReadStream();
            var dto = new ProfileImageUploadDTO
            {
                Stream = stream,
                FileName = image.FileName,
                ContentType = image.ContentType,
                Length = image.Length
            };

            var result = await _userService.UpdateProfileImageAsync(
                GetCurrentUserId(),
                dto);

            return Ok(result);
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.AccountSensitive)]
        [HttpDelete("me")]
        public async Task<IActionResult> DeleteAccount(
            [FromBody] DeleteAccountRequestDTO dto,
            CancellationToken cancellationToken)
        {
            var result = await _accountDeletionService.RequestDeletionAsync(
                GetCurrentUserId(),
                dto,
                cancellationToken);

            return result.PendingOwnershipTransfers
                ? StatusCode(StatusCodes.Status202Accepted, result)
                : Ok(result);
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
        [HttpPost("{userId:guid}/follow")]
        public async Task<IActionResult> Follow(
            Guid userId,
            CancellationToken cancellationToken)
        {
            await _followService.FollowAsync(
                GetCurrentUserId(),
                userId,
                cancellationToken);
            return NoContent();
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
        [HttpDelete("{userId:guid}/unfollow")]
        public async Task<IActionResult> Unfollow(
            Guid userId,
            CancellationToken cancellationToken)
        {
            await _followService.UnfollowAsync(
                GetCurrentUserId(),
                userId,
                cancellationToken);
            return NoContent();
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
        [HttpPost("{userId:guid}/block")]
        public async Task<IActionResult> Block(
            Guid userId,
            CancellationToken cancellationToken)
        {
            await _blockService.BlockAsync(
                GetCurrentUserId(),
                userId,
                cancellationToken);
            return NoContent();
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
        [HttpDelete("{userId:guid}/unblock")]
        public async Task<IActionResult> Unblock(
            Guid userId,
            CancellationToken cancellationToken)
        {
            await _blockService.UnblockAsync(
                GetCurrentUserId(),
                userId,
                cancellationToken);
            return NoContent();
        }

        [Authorize]
        [HttpGet("me/settings/blocked-users")]
        public async Task<IActionResult> GetBlockedUsers(
            [FromQuery] PaginationFilterDTO pagination,
            CancellationToken cancellationToken)
        {
            var result = await _blockService.GetBlockedUsersAsync(
                GetCurrentUserId(),
                pagination,
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [HttpGet("{userId:guid}/follow-status")]
        public async Task<IActionResult> GetFollowStatus(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var result = await _followService.GetFollowStatusAsync(
                GetCurrentUserId(),
                userId,
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [HttpGet("me/follow-requests")]
        public async Task<IActionResult> GetFollowRequests(
            [FromQuery] PaginationFilterDTO pagination,
            [FromQuery] string? search,
            CancellationToken cancellationToken)
        {
            var result = await _followService.GetFollowRequestsAsync(
                GetCurrentUserId(),
                pagination,
                search,
                cancellationToken);

            return Ok(result);
        }


        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
        [HttpPost("me/follow-requests/bulk-accept")]
        public async Task<IActionResult> BulkAcceptFollowRequests(
            [FromBody] FollowRequestBulkActionDTO dto,
            CancellationToken cancellationToken)
        {
            var processedCount = await _followService.AcceptFollowRequestsAsync(
                GetCurrentUserId(),
                dto.RequesterIds,
                cancellationToken);

            return Ok(new FollowRequestBulkResultDTO
            {
                ProcessedCount = processedCount
            });
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
        [HttpPost("me/follow-requests/bulk-reject")]
        public async Task<IActionResult> BulkRejectFollowRequests(
            [FromBody] FollowRequestBulkActionDTO dto,
            CancellationToken cancellationToken)
        {
            var processedCount = await _followService.RejectFollowRequestsAsync(
                GetCurrentUserId(),
                dto.RequesterIds,
                cancellationToken);

            return Ok(new FollowRequestBulkResultDTO
            {
                ProcessedCount = processedCount
            });
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
        [HttpPost("me/follow-requests/{requesterId:guid}/accept-follow")]
        public async Task<IActionResult> AcceptFollowRequest(
            Guid requesterId,
            CancellationToken cancellationToken)
        {
            await _followService.AcceptFollowRequestAsync(
                GetCurrentUserId(),
                requesterId,
                cancellationToken);

            return NoContent();
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
        [HttpDelete("me/follow-requests/{requesterId:guid}/reject-follow")]
        public async Task<IActionResult> RejectFollowRequest(
            Guid requesterId,
            CancellationToken cancellationToken)
        {
            await _followService.RejectFollowRequestAsync(
                GetCurrentUserId(),
                requesterId,
                cancellationToken);

            return NoContent();
        }

        [Authorize]
        [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
        [HttpDelete("me/followers/{followerId:guid}/remove-follower")]
        public async Task<IActionResult> RemoveFollower(
            Guid followerId,
            CancellationToken cancellationToken)
        {
            await _followService.RemoveFollowerAsync(
                GetCurrentUserId(),
                followerId,
                cancellationToken);

            return NoContent();
        }

        [HttpGet("{userId:guid}/followers")]
        public async Task<IActionResult> GetFollowers(
            Guid userId,
            [FromQuery] PaginationFilterDTO pagination,
            CancellationToken cancellationToken)
        {
            var result = await _followService.GetFollowersAsync(
                userId,
                GetOptionalCurrentUserId(),
                pagination,
                cancellationToken);

            return Ok(result);
        }

        [HttpGet("{userId:guid}/following")]
        public async Task<IActionResult> GetFollowing(
            Guid userId,
            [FromQuery] PaginationFilterDTO pagination,
            CancellationToken cancellationToken)
        {
            var result = await _followService.GetFollowingAsync(
                userId,
                GetOptionalCurrentUserId(),
                pagination,
                cancellationToken);

            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userId, out var parsedUserId))
            {
                throw new UnauthorizedAccessException(
                    "İstifadəçi sistemə daxil olmayıb.");
            }

            return parsedUserId;
        }

        private Guid? GetOptionalCurrentUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(userId, out var parsedUserId)
                ? parsedUserId
                : null;
        }
    }
}
