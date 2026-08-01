using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ludora.API.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    private readonly UserPresenceCoordinator _presenceCoordinator;

    public NotificationHub(UserPresenceCoordinator presenceCoordinator)
    {
        _presenceCoordinator = presenceCoordinator;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        await _presenceCoordinator.ConnectedAsync(
            userId,
            PresenceConnectionId(),
            Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetUserId(out var userId))
        {
            await _presenceCoordinator.DisconnectedAsync(
                userId,
                GetSessionId(),
                PresenceConnectionId());
        }

        await base.OnDisconnectedAsync(exception);
    }

    private string PresenceConnectionId() =>
        $"notification:{Context.ConnectionId}";

    private Guid GetUserId()
    {
        if (TryGetUserId(out var userId))
            return userId;

        throw new HubException("İstifadəçi müəyyən edilmədi.");
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }

    private Guid? GetSessionId()
    {
        var value = Context.User?.FindFirstValue("session_id");
        return Guid.TryParse(value, out var sessionId) ? sessionId : null;
    }
}
