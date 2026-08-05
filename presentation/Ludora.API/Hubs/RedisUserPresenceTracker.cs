using Ludora.API.Configuration;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Ludora.API.Hubs;

public sealed class RedisUserPresenceTracker : IUserPresenceTracker
{
    private const string ConnectScript = """
        local cutoff = tonumber(ARGV[3])
        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', cutoff)
        local wasOffline = redis.call('ZCARD', KEYS[1]) == 0
        redis.call('ZADD', KEYS[1], ARGV[2], ARGV[1])
        redis.call('EXPIRE', KEYS[1], ARGV[4])
        if wasOffline then return 1 else return 0 end
        """;

    private const string DisconnectScript = """
        redis.call('ZREM', KEYS[1], ARGV[1])
        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', tonumber(ARGV[2]))
        local remaining = redis.call('ZCARD', KEYS[1])
        if remaining == 0 then
            redis.call('DEL', KEYS[1])
            return 1
        end
        return 0
        """;

    private readonly IDatabase _database;
    private readonly ILogger<RedisUserPresenceTracker> _logger;
    private readonly PresenceSettings _settings;
    private readonly string _keyPrefix;
    private readonly object _localLock = new();
    private readonly Dictionary<Guid, HashSet<string>> _localConnections = [];

    public RedisUserPresenceTracker(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<CacheSettings> cacheOptions,
        IOptions<PresenceSettings> presenceOptions,
        ILogger<RedisUserPresenceTracker> logger)
    {
        _database = connectionMultiplexer.GetDatabase();
        _logger = logger;
        _settings = presenceOptions.Value;
        _keyPrefix = $"{cacheOptions.Value.InstanceName}presence:{{users}}:";
    }

    public async Task<bool> ConnectAsync(
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var localWasOffline = AddLocalConnection(userId, connectionId);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        try
        {
            var result = await _database.ScriptEvaluateAsync(
                ConnectScript,
                [GetKey(userId)],
                [
                    connectionId,
                    now,
                    now - TimeSpan.FromSeconds(_settings.OfflineAfterSeconds).TotalMilliseconds,
                    _settings.OfflineAfterSeconds
                ]);

            return (long)result == 1;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Redis presence connect failed for user {UserId}; local fallback is used.",
                userId);
            return localWasOffline;
        }
    }

    public async Task<bool> DisconnectAsync(
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var localBecameOffline = RemoveLocalConnection(userId, connectionId);
        var cutoff = DateTimeOffset.UtcNow
            .AddSeconds(-_settings.OfflineAfterSeconds)
            .ToUnixTimeMilliseconds();

        try
        {
            var result = await _database.ScriptEvaluateAsync(
                DisconnectScript,
                [GetKey(userId)],
                [connectionId, cutoff]);

            return (long)result == 1;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Redis presence disconnect failed for user {UserId}; local fallback is used.",
                userId);
            return localBecameOffline;
        }
    }

    public async Task<bool> IsOnlineAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var onlineUsers = await GetOnlineUserIdsAsync([userId], cancellationToken);
        return onlineUsers.Contains(userId);
    }

    public async Task<IReadOnlySet<Guid>> GetOnlineUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        cancellationToken.ThrowIfCancellationRequested();

        var ids = userIds
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
            return new HashSet<Guid>();

        try
        {
            var batch = _database.CreateBatch();
            var checks = ids
                .Select(userId => new
                {
                    UserId = userId,
                    Exists = batch.KeyExistsAsync(GetKey(userId))
                })
                .ToArray();

            batch.Execute();
            await Task.WhenAll(checks.Select(check => check.Exists));

            return checks
                .Where(check => check.Exists.Result)
                .Select(check => check.UserId)
                .ToHashSet();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Redis presence lookup failed for {UserCount} users; local fallback is used.",
                ids.Length);

            return ids
                .Where(IsLocallyOnline)
                .ToHashSet();
        }
    }

    public async Task RefreshLocalConnectionsAsync(
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoff = now - (long)TimeSpan
            .FromSeconds(_settings.OfflineAfterSeconds)
            .TotalMilliseconds;
        var operations = new List<Task>();

        List<(Guid UserId, string ConnectionId)> localConnections;
        lock (_localLock)
        {
            localConnections = _localConnections
                .SelectMany(pair => pair.Value.Select(connectionId =>
                    (pair.Key, connectionId)))
                .ToList();
        }

        foreach (var (userId, connectionId) in localConnections)
        {
            operations.Add(_database.ScriptEvaluateAsync(
                ConnectScript,
                [GetKey(userId)],
                [
                    connectionId,
                    now,
                    cutoff,
                    _settings.OfflineAfterSeconds
                ]));
        }

        if (operations.Count == 0)
            return;

        try
        {
            await Task.WhenAll(operations).WaitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Redis presence heartbeat failed for {ConnectionCount} local connections.",
                operations.Count);
        }
    }

    private bool AddLocalConnection(Guid userId, string connectionId)
    {
        lock (_localLock)
        {
            var wasOffline = !_localConnections.TryGetValue(
                userId,
                out var connections) || connections.Count == 0;

            if (connections is null)
            {
                connections = [];
                _localConnections[userId] = connections;
            }

            connections.Add(connectionId);
            return wasOffline;
        }
    }

    private bool RemoveLocalConnection(Guid userId, string connectionId)
    {
        lock (_localLock)
        {
            if (!_localConnections.TryGetValue(userId, out var connections))
                return false;

            connections.Remove(connectionId);
            if (connections.Count > 0)
                return false;

            _localConnections.Remove(userId);
            return true;
        }
    }

    private bool IsLocallyOnline(Guid userId)
    {
        lock (_localLock)
        {
            return _localConnections.TryGetValue(userId, out var connections) &&
                   connections.Count > 0;
        }
    }

    private RedisKey GetKey(Guid userId) => $"{_keyPrefix}{userId:N}";
}
