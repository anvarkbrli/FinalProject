using Ludora.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Ludora.Infrastructure.Caching;

public sealed class MemoryAppCache : IAppCache
{
    private readonly IMemoryCache _memoryCache;

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

    public Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _memoryCache.Remove(key);
        return Task.CompletedTask;
    }
}
