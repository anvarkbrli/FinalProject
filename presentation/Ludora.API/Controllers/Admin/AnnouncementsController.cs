using System.Security.Claims;
using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Admin;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers.Admin;

[Route("api/announcements")]
[ApiController]
public sealed class AnnouncementsController : ControllerBase
{
    private readonly IAnnouncementService _announcementService;

    public AnnouncementsController(IAnnouncementService announcementService)
    {
        _announcementService = announcementService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDTO<AnnouncementDTO>>> GetPublished(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _announcementService.GetPublishedAsync(
            GetOptionalCurrentUserId(),
            pageNumber,
            pageSize,
            cancellationToken));
    }

    [HttpGet("{announcementId:int}")]
    public async Task<ActionResult<AnnouncementDTO>> GetPublishedById(
        int announcementId,
        CancellationToken cancellationToken)
    {
        return Ok(await _announcementService.GetPublishedByIdAsync(
            GetOptionalCurrentUserId(),
            announcementId,
            cancellationToken));
    }

    [Authorize]
    [HttpPost("{announcementId:int}/view")]
    public async Task<IActionResult> MarkViewed(
        int announcementId,
        CancellationToken cancellationToken)
    {
        await _announcementService.MarkViewedAsync(
            GetRequiredCurrentUserId(),
            announcementId,
            cancellationToken);

        return NoContent();
    }

    private Guid GetRequiredCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("İstifadəçi müəyyən edilmədi.");
    }

    private Guid? GetOptionalCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
