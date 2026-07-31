using Ludora.API.Security;
using Ludora.Application.Common;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers.Admin;

[Authorize(Policy = CapabilityNames.ViewModerationAudit)]
[EnableRateLimiting(RateLimitPolicyNames.Moderation)]
[Route("api/admin/moderation-audit")]
[ApiController]
public sealed class AdminModerationAuditController : ControllerBase
{
    private readonly IAdminModerationAuditService _auditService;

    public AdminModerationAuditController(
        IAdminModerationAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDTO<ModeratorActionLogDTO>>> GetAll(
        [FromQuery] AdminModerationAuditFilterDTO filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _auditService.GetAllAsync(filter, cancellationToken));
    }

    [HttpGet("reports/{reportId:long}")]
    public async Task<ActionResult<IReadOnlyList<ModeratorActionLogDTO>>> GetByReport(
        long reportId,
        CancellationToken cancellationToken)
    {
        return Ok(await _auditService.GetByReportAsync(
            reportId,
            cancellationToken));
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult<IReadOnlyList<ModeratorActionLogDTO>>> GetByUser(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return Ok(await _auditService.GetByUserAsync(
            userId,
            cancellationToken));
    }
}
