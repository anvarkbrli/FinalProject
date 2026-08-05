using Ludora.API.Security;
using Ludora.API.Models;
using System.Security.Claims;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers.User;

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
    [EnableRateLimiting(RateLimitPolicyNames.Uploads)]
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PostDTO>> Create(
        [FromForm] PostCreateFormRequest request,
        CancellationToken cancellationToken)
    {
        var image = request.Image;
        await using var imageStream = image is { Length: > 0 }
            ? image.OpenReadStream()
            : null;

        var result = await _postService.CreateAsync(
            GetCurrentUserId(),
            new PostCreateDTO
            {
                Content = request.Content,
                Image = CreateImageUpload(image, imageStream)
            },
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
    public async Task<ActionResult<PagedResultDTO<PostDTO>>> GetUserPosts(
        Guid userId,
        [FromQuery] PaginationFilterDTO pagination,
        CancellationToken cancellationToken)
    {
        var result = await _postService.GetUserPostsAsync(
            userId,
            GetOptionalCurrentUserId(),
            pagination,
            cancellationToken);

        return Ok(result);
    }

    [Authorize]
    [HttpGet("feed")]
    public async Task<ActionResult<PagedResultDTO<PostDTO>>> GetFeed(
        [FromQuery] PaginationFilterDTO pagination,
        CancellationToken cancellationToken)
    {
        var result = await _postService.GetFeedAsync(
            GetCurrentUserId(),
            pagination,
            cancellationToken);

        return Ok(result);
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.Uploads)]
    [HttpPut("{postId:long}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PostDTO>> Update(
        long postId,
        [FromForm] PostUpdateFormRequest request,
        CancellationToken cancellationToken)
    {
        var image = request.Image;
        await using var imageStream = image is { Length: > 0 }
            ? image.OpenReadStream()
            : null;

        var result = await _postService.UpdateAsync(
            GetCurrentUserId(),
            postId,
            new PostUpdateDTO
            {
                Content = request.Content,
                Image = CreateImageUpload(image, imageStream),
                RemoveImage = request.RemoveImage
            },
            cancellationToken);

        return Ok(result);
    }

    [Authorize]
    [EnableRateLimiting(RateLimitPolicyNames.SocialWrite)]
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

    private static PostImageUploadDTO? CreateImageUpload(
        IFormFile? image,
        Stream? imageStream)
    {
        if (image is null || imageStream is null || image.Length <= 0)
            return null;

        return new PostImageUploadDTO
        {
            Stream = imageStream,
            FileName = image.FileName,
            ContentType = image.ContentType,
            Length = image.Length
        };
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
            throw new UnauthorizedAccessException(
                "İstifadəçi müəyyən edilmədi.");

        return userId;
    }

    private Guid? GetOptionalCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}
