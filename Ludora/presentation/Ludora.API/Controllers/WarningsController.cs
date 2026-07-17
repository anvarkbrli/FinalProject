using Ludora.API.Extensions;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers;

[Authorize]
[Route("api/warnings")]
[ApiController]
public sealed class WarningsController : ControllerBase
{
    private readonly IUserWarningService _warningService;

    public WarningsController(IUserWarningService warningService)
    {
        _warningService = warningService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDTO<UserWarningDTO>>> GetMine(
        [FromQuery] bool? isRead,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _warningService.GetMineAsync(
            User.GetRequiredUserId(),
            isRead,
            pageNumber,
            pageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpPut("{warningId:int}/read")]
    public async Task<ActionResult<UserWarningDTO>> MarkAsRead(
        int warningId,
        CancellationToken cancellationToken)
    {
        var result = await _warningService.MarkAsReadAsync(
            User.GetRequiredUserId(),
            warningId,
            cancellationToken);

        return Ok(result);
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(
        CancellationToken cancellationToken)
    {
        await _warningService.MarkAllAsReadAsync(
            User.GetRequiredUserId(),
            cancellationToken);

        return NoContent();
    }
}
