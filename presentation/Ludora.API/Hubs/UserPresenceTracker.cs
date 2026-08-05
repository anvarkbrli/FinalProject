using Ludora.Application.Interfaces;

namespace Ludora.API.Hubs;

public sealed class UserPresenceTracker : IUserPresenceTracker
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, HashSet<string>> _connections = [];

    public Task<bool> ConnectAsync(
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
            return Task.FromResult(wasOffline);
        }
    }

    public Task<bool> DisconnectAsync(
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (!_connections.TryGetValue(userId, out var userConnections))
                return Task.FromResult(false);

            userConnections.Remove(connectionId);
            if (userConnections.Count > 0)
                return Task.FromResult(false);

            _connections.Remove(userId);
            return Task.FromResult(true);
        }
    }

    public Task<bool> IsOnlineAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            return Task.FromResult(
                _connections.TryGetValue(userId, out var userConnections) &&
                userConnections.Count > 0);
        }
    }

    public Task<IReadOnlySet<Guid>> GetOnlineUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            IReadOnlySet<Guid> result = userIds
                .Where(userId =>
                    _connections.TryGetValue(userId, out var userConnections) &&
                    userConnections.Count > 0)
                .ToHashSet();

            return Task.FromResult(result);
        }
    }
}
