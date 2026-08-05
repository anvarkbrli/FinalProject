using Ludora.Application.Interfaces;

namespace Ludora.API.Hubs;

public sealed class UserPresenceTracker : IUserPresenceTracker
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, HashSet<string>> _connections = [];

    public bool Connect(Guid userId, string connectionId)
    {
        lock (_lock)
        {
            var wasOffline = !_connections.TryGetValue(userId, out var userConnections) ||
                             userConnections.Count == 0;

            if (userConnections is null)
            {
                userConnections = [];
                _connections[userId] = userConnections;
            }

            userConnections.Add(connectionId);
            return wasOffline;
        }
    }

    public bool Disconnect(Guid userId, string connectionId)
    {
        lock (_lock)
        {
            if (!_connections.TryGetValue(userId, out var userConnections))
                return false;

            userConnections.Remove(connectionId);
            if (userConnections.Count > 0)
                return false;

            _connections.Remove(userId);
            return true;
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
