using Ludora.Application.Common;
using Ludora.Application.DTOs;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers.Admin;

[Authorize(Roles = RoleNames.Admin)]
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
