using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Ludora.Persistence.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ludora.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IFollowService _followService;
        private readonly IBlockService _blockService;

        public UsersController(IUserService userService, IFollowService followService, IBlockService blockService)
        {
            _userService = userService;
            _followService = followService;
            _blockService = blockService;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var result = await _userService.GetCurrentUserAsync(GetCurrentUserId());

            return Ok(result);
        }

        [Authorize]
        [HttpGet("me/settings")]
        public async Task<IActionResult> GetSettings()
        {
            var result = await _userService.GetSettingsAsync(GetCurrentUserId());

            return Ok(result);
        }

        [Authorize]
        [HttpPatch("me/settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateUserSettingsDTO dto)
        {
            var result = await _userService.UpdateSettingsAsync(GetCurrentUserId(), dto);

            return Ok(result);
        }

        [HttpGet("{userName}")]
        public async Task<IActionResult> GetByUserName(string userName)
        {
            var result = await _userService.GetByUserNameAsync(userName, GetOptionalCurrentUserId());

            return Ok(result);
        }

        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDTO dto)
        {
            var result = await _userService.UpdateProfileAsync(GetCurrentUserId(), dto);

            return Ok(result);
        }

        [Authorize]
        [HttpPut("me/profile-image")]
        public async Task<IActionResult> UpdateProfileImage(IFormFile image)
        {
            await using var stream = image.OpenReadStream();
            var dto = new ProfileImageUploadDTO
            {
                Stream = stream,
                FileName = image.FileName,
                ContentType = image.ContentType,
                Length = image.Length
            };

            var result = await _userService.UpdateProfileImageAsync(GetCurrentUserId(), dto);

            return Ok(result);
        }

        [Authorize]
        [HttpPost("{userId:guid}/follow")]
        public async Task<IActionResult> Follow(Guid userId)
        {
            await _followService.FollowAsync(GetCurrentUserId(), userId);

            return NoContent();
        }

        [Authorize]
        [HttpDelete("{userId:guid}/unfollow")]
        public async Task<IActionResult> Unfollow(Guid userId)
        {
            await _followService.UnfollowAsync(GetCurrentUserId(), userId);

            return NoContent();
        }
        [Authorize]
        [HttpPost("{userId:guid}/block")]
        public async Task<IActionResult> Block(Guid userId)
        {
            await _blockService.BlockAsync(GetCurrentUserId(), userId);

            return NoContent();
        }

        [Authorize]
        [HttpDelete("{userId:guid}/unblock")]
        public async Task<IActionResult> Unblock(Guid userId)
        {
            await _blockService.UnblockAsync(GetCurrentUserId(), userId);

            return NoContent();
        }

        [Authorize]
        [HttpGet("me/settings/blocked-users")]
        public async Task<IActionResult> GetBlockedUsers()
        {
            var result = await _blockService.GetBlockedUsersAsync(GetCurrentUserId());

            return Ok(result);
        }

        [Authorize]
        [HttpGet("{userId:guid}/follow-status")]
        public async Task<IActionResult> GetFollowStatus(Guid userId)
        {
            var result = await _followService.GetFollowStatusAsync(GetCurrentUserId(), userId);

            return Ok(result);
        }

        [Authorize]
        [HttpGet("me/follow-requests")]
        public async Task<IActionResult> GetFollowRequests()
        {
            var result = await _followService.GetFollowRequestsAsync(GetCurrentUserId());

            return Ok(result);
        }

        [Authorize]
        [HttpPost("me/follow-requests/{requesterId:guid}/accept-follow")]
        public async Task<IActionResult> AcceptFollowRequest(Guid requesterId)
        {
            await _followService.AcceptFollowRequestAsync(GetCurrentUserId(), requesterId);

            return NoContent();
        }

        [Authorize]
        [HttpDelete("me/follow-requests/{requesterId:guid}/reject-follow")]
        public async Task<IActionResult> RejectFollowRequest(Guid requesterId)
        {
            await _followService.RejectFollowRequestAsync(GetCurrentUserId(), requesterId);

            return NoContent();
        }

        [Authorize]
        [HttpDelete("me/followers/{followerId:guid}/remove-follower")]
        public async Task<IActionResult> RemoveFollower(Guid followerId)
        {
            await _followService.RemoveFollowerAsync(GetCurrentUserId(), followerId);

            return NoContent();
        }

        [HttpGet("{userId:guid}/followers")]
        public async Task<IActionResult> GetFollowers(Guid userId)
        {
            var result = await _followService.GetFollowersAsync(userId, GetOptionalCurrentUserId());

            return Ok(result);
        }

        [HttpGet("{userId:guid}/following")]
        public async Task<IActionResult> GetFollowing(Guid userId)
        {
            var result = await _followService.GetFollowingAsync(userId, GetOptionalCurrentUserId());

            return Ok(result);
        }

        private Guid GetCurrentUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userId, out var parsedUserId))
                throw new UnauthorizedAccessException("İstifadəçi sistemə daxil olmayıb.");

            return parsedUserId;
        }

        private Guid? GetOptionalCurrentUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(userId, out var parsedUserId) ? parsedUserId : null;
        }
    }
}
