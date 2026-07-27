using Ludora.Application.Interfaces;

namespace Ludora.API.Hubs;

public sealed class UserPresenceTracker : IUserPresenceTracker
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, HashSet<string>> _connections = [];

    public void Connect(Guid userId, string connectionId)
    {
        lock (_lock)
        {
            if (!_connections.TryGetValue(userId, out var userConnections))
            {
                userConnections = [];
                _connections[userId] = userConnections;
            }

            userConnections.Add(connectionId);
        }
    }

    public void Disconnect(Guid userId, string connectionId)
    {
        lock (_lock)
        {
            if (!_connections.TryGetValue(userId, out var userConnections))
                return;

            userConnections.Remove(connectionId);

            if (userConnections.Count == 0)
                _connections.Remove(userId);
        }
    }

    public bool IsOnline(Guid userId)
    {
        lock (_lock)
        {
            return _connections.TryGetValue(userId, out var userConnections) &&
                   userConnections.Count > 0;
        }
    }
}
