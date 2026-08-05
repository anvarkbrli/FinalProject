using System.Collections.Concurrent;
using Ludora.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Ludora.Infrastructure.Caching;

public sealed class MemoryAppCache : IAppCache
{
    private readonly IMemoryCache _memoryCache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

    public MemoryAppCache(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            _memoryCache.TryGetValue(key, out T? value)
                ? value
                : default);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan absoluteExpiration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(value);

        if (absoluteExpiration <= TimeSpan.Zero)
        {
            _memoryCache.Remove(key);
            return Task.CompletedTask;
        }

        _memoryCache.Set(key, value, absoluteExpiration);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan absoluteExpiration,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var keyLock = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(cancellationToken);

        try
        {
            cached = await GetAsync<T>(key, cancellationToken);
            if (cached is not null)
                return cached;

            var created = await factory(cancellationToken);
            ArgumentNullException.ThrowIfNull(created);
            await SetAsync(key, created, absoluteExpiration, cancellationToken);
            return created;
        }
        finally
        {
            keyLock.Release();
            if (keyLock.CurrentCount == 1)
                _keyLocks.TryRemove(new KeyValuePair<string, SemaphoreSlim>(key, keyLock));
        }
    }

    public Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _memoryCache.Remove(key);
        return Task.CompletedTask;
    }
}
