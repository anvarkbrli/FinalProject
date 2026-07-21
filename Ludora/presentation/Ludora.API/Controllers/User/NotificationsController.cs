using System.Security.Claims;
using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Notification;
using Ludora.Application.Interfaces;
using Ludora.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ludora.API.Controllers.User;

[Authorize]
[Route("api/notifications")]
[ApiController]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDTO<NotificationDTO>>> GetMine(
        [FromQuery] NotificationFilterDTO filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _notificationService.GetMineAsync(
            GetCurrentUserId(),
            filter,
            cancellationToken));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadNotificationCountDTO>> GetUnreadCount(
        CancellationToken cancellationToken)
    {
        return Ok(await _notificationService.GetUnreadCountAsync(
            GetCurrentUserId(),
            cancellationToken));
    }

    [HttpPatch("{notificationId:long}/read")]
    public async Task<ActionResult<NotificationDTO>> MarkAsRead(
        long notificationId,
        CancellationToken cancellationToken)
    {
        return Ok(await _notificationService.MarkAsReadAsync(
            GetCurrentUserId(),
            notificationId,
            cancellationToken));
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        await _notificationService.MarkAllAsReadAsync(
            GetCurrentUserId(),
            cancellationToken);

        return NoContent();
    }

    [HttpDelete("{notificationId:long}")]
    public async Task<IActionResult> Delete(
        long notificationId,
        CancellationToken cancellationToken)
    {
        await _notificationService.DeleteAsync(
            GetCurrentUserId(),
            notificationId,
            cancellationToken);

        return NoContent();
    }

    [HttpGet("settings")]
    public async Task<ActionResult<NotificationSettingsDTO>> GetSettings(
        CancellationToken cancellationToken)
    {
        return Ok(await _notificationService.GetSettingsAsync(
            GetCurrentUserId(),
            cancellationToken));
    }

    [HttpPut("settings/{type}")]
    public async Task<ActionResult<NotificationPreferenceDTO>> UpdatePreference(
        NotificationType type,
        [FromBody] UpdateNotificationPreferenceDTO dto,
        CancellationToken cancellationToken)
    {
        return Ok(await _notificationService.UpdatePreferenceAsync(
            GetCurrentUserId(),
            type,
            dto,
            cancellationToken));
    }

    [HttpDelete("settings/reset")]
    public async Task<IActionResult> ResetPreferences(CancellationToken cancellationToken)
    {
        await _notificationService.ResetPreferencesAsync(
            GetCurrentUserId(),
            cancellationToken);

        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("İstifadəçi müəyyən edilmədi.");

        return userId;
    }
}
