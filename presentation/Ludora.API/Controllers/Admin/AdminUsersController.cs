using Ludora.API.Security;
using Ludora.API.Extensions;
using Ludora.Application.Common;
using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Admin;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ludora.API.Controllers.Admin;

[Authorize(Policy = CapabilityNames.ViewUsers)]
[EnableRateLimiting(RateLimitPolicyNames.Moderation)]
[Route("api/admin/users")]
[ApiController]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;

    public AdminUsersController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDTO<AdminUserListItemDTO>>> GetAll([FromQuery] AdminUserFilterDTO filter, CancellationToken cancellationToken)
    {
        return Ok(await _adminUserService.GetAllAsync(filter, cancellationToken));
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<AdminUserDetailDTO>> GetById(Guid userId, CancellationToken cancellationToken)
    {
        return Ok(await _adminUserService.GetByIdAsync(userId, cancellationToken));
    }

    [HttpGet("{userId:guid}/ban-history")]
    public async Task<ActionResult<AdminUserBanHistoryDTO>> GetBanHistory(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _adminUserService.GetBanHistoryAsync(userId, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{userId:guid}/warnings")]
    public async Task<ActionResult<AdminUserWarningHistoryDTO>> GetWarningHistory(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminUserService.GetWarningHistoryAsync(
            userId,
            cancellationToken));
    }

    [HttpPost("{userId:guid}/warnings")]
    [Authorize(Policy = CapabilityNames.WarnUsers)]
    public async Task<ActionResult<UserWarningDTO>> Warn(
        Guid userId,
        [FromBody] CreateUserWarningDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await _adminUserService.WarnAsync(
            User.GetRequiredUserId(),
            userId,
            dto,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{userId:guid}/ban")]
    [Authorize(Policy = CapabilityNames.TemporarilyBanUsers)]
    public async Task<ActionResult<AdminUserDetailDTO>> Ban(Guid userId, [FromBody] BanUserDTO dto, CancellationToken cancellationToken)
    {
        var result = await _adminUserService.BanAsync(
            User.GetRequiredUserId(),
            userId,
            dto,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{userId:guid}/unban")]
    [Authorize(Policy = CapabilityNames.UnbanTemporaryUsers)]
    public async Task<ActionResult<AdminUserDetailDTO>> Unban(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _adminUserService.UnbanAsync(
            User.GetRequiredUserId(),
            userId,
            cancellationToken);

        return Ok(result);
    }

    [HttpPut("{userId:guid}/role")]
    [Authorize(Policy = CapabilityNames.ManageRoles)]
    public async Task<ActionResult<AdminUserDetailDTO>> ChangeRole(
        Guid userId,
        [FromBody] ChangeUserRoleDTO dto,
        CancellationToken cancellationToken)
    {
        var result = await _adminUserService.ChangeRoleAsync(
            User.GetRequiredUserId(),
            userId,
            dto,
            cancellationToken);

        return Ok(result);
    }
}
