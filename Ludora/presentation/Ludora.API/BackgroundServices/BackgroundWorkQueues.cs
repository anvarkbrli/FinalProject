using System.Collections.Concurrent;
using System.Threading.Channels;
using Ludora.Application.Interfaces;

namespace Ludora.API.BackgroundServices;

public sealed class BadgeEvaluationQueue : IBadgeEvaluationQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(2000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        });
    private readonly ConcurrentDictionary<Guid, byte> _queued = new();

    public bool Enqueue(Guid userId)
    {
        if (userId == Guid.Empty || !_queued.TryAdd(userId, 0))
            return false;

        if (_channel.Writer.TryWrite(userId))
            return true;

        _queued.TryRemove(userId, out _);
        return false;
    }

    public async IAsyncEnumerable<Guid> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        await foreach (var userId in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            _queued.TryRemove(userId, out _);
            yield return userId;
        }
    }
}

public sealed class SteamSyncQueue : ISteamSyncQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(500)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        });
    private readonly ConcurrentDictionary<Guid, byte> _queued = new();

    public bool Enqueue(Guid userId)
    {
        if (userId == Guid.Empty || !_queued.TryAdd(userId, 0))
            return false;

        if (_channel.Writer.TryWrite(userId))
            return true;

        _queued.TryRemove(userId, out _);
        return false;
    }

    public async IAsyncEnumerable<Guid> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        await foreach (var userId in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            _queued.TryRemove(userId, out _);
            yield return userId;
        }
    }
}
