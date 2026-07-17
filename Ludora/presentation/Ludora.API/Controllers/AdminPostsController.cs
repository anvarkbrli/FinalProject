using Ludora.API.Extensions;
using Ludora.Application.Common;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers;

[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/posts")]
[ApiController]
public sealed class AdminPostsController : ControllerBase
{
    private readonly IAdminPostService _adminPostService;

    public AdminPostsController(IAdminPostService adminPostService)
    {
        _adminPostService = adminPostService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDTO<AdminPostDTO>>> GetAll(
        [FromQuery] AdminPostFilterDTO filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminPostService.GetAllAsync(filter, cancellationToken));
    }

    [HttpGet("{postId:long}")]
    public async Task<ActionResult<AdminPostDTO>> GetById(
        long postId,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminPostService.GetByIdAsync(postId, cancellationToken));
    }

    [HttpDelete("{postId:long}")]
    public async Task<IActionResult> Delete(
        long postId,
        [FromBody] ModeratePostDTO dto,
        CancellationToken cancellationToken)
    {
        await _adminPostService.DeleteAsync(
            User.GetRequiredUserId(),
            postId,
            dto,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{postId:long}/restore")]
    public async Task<ActionResult<AdminPostDTO>> Restore(
        long postId,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminPostService.RestoreAsync(postId, cancellationToken));
    }
}
