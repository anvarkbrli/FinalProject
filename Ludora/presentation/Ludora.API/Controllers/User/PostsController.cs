using System.Security.Claims;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers.User
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;

        public PostsController(IPostService postService)
        {
            _postService = postService;
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<PostDTO>> Create(
            [FromBody] PostCreateDTO dto,
            CancellationToken cancellationToken)
        {
            var result = await _postService.CreateAsync(
                GetCurrentUserId(),
                dto,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { postId = result.Id },
                result);
        }

        [HttpGet("{postId:long}")]
        public async Task<ActionResult<PostDTO>> GetById(
            long postId,
            CancellationToken cancellationToken)
        {
            var result = await _postService.GetByIdAsync(
                postId,
                GetOptionalCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }

        [HttpGet("user/{userId:guid}")]
        public async Task<ActionResult<IReadOnlyList<PostDTO>>> GetUserPosts(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var result = await _postService.GetUserPostsAsync(
                userId,
                GetOptionalCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [HttpGet("feed")]
        public async Task<ActionResult<IReadOnlyList<PostDTO>>> GetFeed(
            CancellationToken cancellationToken)
        {
            var result = await _postService.GetFeedAsync(
                GetCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [HttpPut("{postId:long}")]
        public async Task<ActionResult<PostDTO>> Update(
            long postId,
            [FromBody] PostUpdateDTO dto,
            CancellationToken cancellationToken)
        {
            var result = await _postService.UpdateAsync(
                GetCurrentUserId(),
                postId,
                dto,
                cancellationToken);

            return Ok(result);
        }

        [Authorize]
        [HttpDelete("{postId:long}")]
        public async Task<IActionResult> Delete(
            long postId,
            CancellationToken cancellationToken)
        {
            await _postService.DeleteAsync(
                GetCurrentUserId(),
                postId,
                cancellationToken);

            return NoContent();
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
}



