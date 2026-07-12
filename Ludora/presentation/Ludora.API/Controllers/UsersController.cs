using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
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

        public UsersController(IUserService userService, IFollowService followService)
        {
            _userService = userService;
            _followService = followService;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var result = await _userService.GetCurrentUserAsync(GetCurrentUserId());

            return Ok(result);
        }

        [HttpGet("{userName}")]
        public async Task<IActionResult> GetByUserName(string userName)
        {
            var result = await _userService.GetByUserNameAsync(userName);

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
            var dto = new ProfileImageUploadDTO
            {
                Stream = image.OpenReadStream(),
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
        [HttpDelete("{userId:guid}/follow")]
        public async Task<IActionResult> Unfollow(Guid userId)
        {
            await _followService.UnfollowAsync(GetCurrentUserId(), userId);

            return NoContent();
        }

        [Authorize]
        [HttpGet("{userId:guid}/follow-status")]
        public async Task<IActionResult> GetFollowStatus(Guid userId)
        {
            var result = await _followService.GetFollowStatusAsync(GetCurrentUserId(), userId);

            return Ok(result);
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
