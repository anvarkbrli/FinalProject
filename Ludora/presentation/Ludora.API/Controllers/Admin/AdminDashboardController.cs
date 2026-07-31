using Ludora.API.Security;
using Ludora.Application.Common;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers.Admin;

[Authorize(Policy = CapabilityNames.ViewDashboard)]
[EnableRateLimiting(RateLimitPolicyNames.Moderation)]
[Route("api/admin/dashboard")]
[ApiController]
public sealed class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _dashboardService;

    public AdminDashboardController(IAdminDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<ActionResult<AdminDashboardDTO>> Get(
        CancellationToken cancellationToken)
    {
        return Ok(await _dashboardService.GetAsync(cancellationToken));
    }
}
