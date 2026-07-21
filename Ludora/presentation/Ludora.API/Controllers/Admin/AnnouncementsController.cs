using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Admin;
using Ludora.Application.Interfaces;
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
            announcementId,
            cancellationToken));
    }
}
