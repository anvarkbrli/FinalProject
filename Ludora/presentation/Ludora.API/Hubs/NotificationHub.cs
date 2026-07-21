using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ludora.API.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
}
