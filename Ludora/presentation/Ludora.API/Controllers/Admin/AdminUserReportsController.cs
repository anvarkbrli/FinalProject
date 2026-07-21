using Ludora.API.Extensions;
using Ludora.Application.Common;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers.Admin;

[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/user-reports")]
[ApiController]
public sealed class AdminUserReportsController : ControllerBase
{
    private readonly IAdminUserReportService _adminUserReportService;

    public AdminUserReportsController(
        IAdminUserReportService adminUserReportService)
    {
        _adminUserReportService = adminUserReportService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDTO<AdminReportedUserListItemDTO>>> GetReportedUsers(
        [FromQuery] AdminUserReportFilterDTO filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminUserReportService.GetReportedUsersAsync(
            filter,
            cancellationToken));
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult<AdminReportedUserDetailDTO>> GetReportedUser(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminUserReportService.GetReportedUserAsync(
            userId,
            cancellationToken));
    }

    [HttpGet("{reportId:long}/evidence")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetEvidence(
        long reportId,
        CancellationToken cancellationToken)
    {
        var file = await _adminUserReportService.GetEvidenceAsync(
            reportId,
            cancellationToken);

        return File(file.Content, file.ContentType);
    }

    [HttpPatch("{reportId:long}/status")]
    public async Task<ActionResult<AdminUserReportItemDTO>> UpdateStatus(
        long reportId,
        [FromBody] AdminUserReportStatusUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminUserReportService.UpdateStatusAsync(
            User.GetRequiredUserId(),
            reportId,
            dto,
            cancellationToken));
    }
}
