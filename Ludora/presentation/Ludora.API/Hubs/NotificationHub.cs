using System.Security.Claims;
using Ludora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ludora.API.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    private readonly IUserPresenceTracker _presenceTracker;

    public NotificationHub(IUserPresenceTracker presenceTracker)
    {
        _presenceTracker = presenceTracker;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        _presenceTracker.Connect(userId, PresenceConnectionId());
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetUserId(out var userId))
            _presenceTracker.Disconnect(userId, PresenceConnectionId());

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
}
