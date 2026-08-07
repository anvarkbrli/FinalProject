using Ludora.API.Configuration;
using Ludora.Application.Interfaces;

using Microsoft.Extensions.Options;

namespace Ludora.API.Hubs
{
    public sealed class UserPresenceTracker : IUserPresenceTracker
    {
        private readonly object _lock = new();
        private readonly Dictionary<Guid, Dictionary<string, DateTime>> _connections = [];
        private readonly TimeSpan _offlineAfter;

        public UserPresenceTracker(IOptions<PresenceSettings> options)
        {
            var seconds = Math.Clamp(options.Value.OfflineAfterSeconds, 30, 600);
            _offlineAfter = TimeSpan.FromSeconds(seconds);
        }

        public Task<bool> ConnectAsync(
            Guid userId,
            string connectionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                PruneUserUnsafe(userId, now);
                var wasOffline = !_connections.TryGetValue(userId, out var userConnections) ||
                                 userConnections.Count == 0;

                if (userConnections is null)
                {
                    userConnections = [];
                    _connections[userId] = userConnections;
                }

                userConnections[connectionId] = now;
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

        public Task HeartbeatAsync(
            Guid userId,
            string connectionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                PruneUserUnsafe(userId, now);
                if (!_connections.TryGetValue(userId, out var userConnections))
                {
                    userConnections = [];
                    _connections[userId] = userConnections;
                }

                userConnections[connectionId] = now;
            }

            return Task.CompletedTask;
        }

        public Task<bool> IsOnlineAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_lock)
            {
                PruneUserUnsafe(userId, DateTime.UtcNow);
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
                var now = DateTime.UtcNow;
                var result = new HashSet<Guid>();
                foreach (var userId in userIds.Where(id => id != Guid.Empty).Distinct())
                {
                    PruneUserUnsafe(userId, now);
                    if (_connections.TryGetValue(userId, out var userConnections) &&
                        userConnections.Count > 0)
                    {
                        result.Add(userId);
                    }
                }

                return Task.FromResult<IReadOnlySet<Guid>>(result);
            }
        }

        private void PruneUserUnsafe(Guid userId, DateTime now)
        {
            if (!_connections.TryGetValue(userId, out var userConnections))
                return;

            var cutoff = now - _offlineAfter;
            foreach (var staleConnectionId in userConnections
                         .Where(pair => pair.Value < cutoff)
                         .Select(pair => pair.Key)
                         .ToList())
            {
                userConnections.Remove(staleConnectionId);
            }

            if (userConnections.Count == 0)
                _connections.Remove(userId);
        }
    }
}
