using System.Text.Json;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.Caching;

public sealed class HybridAppCache : IAppCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly CacheSettings _settings;
    private readonly ILogger<HybridAppCache> _logger;

    public HybridAppCache(
        IDistributedCache distributedCache,
        IMemoryCache memoryCache,
        IOptions<CacheSettings> options,
        ILogger<HybridAppCache> logger)
    {
        _distributedCache = distributedCache;
        _memoryCache = memoryCache;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(key, out T? localValue))
            return localValue;

        try
        {
            var payload = await _distributedCache.GetAsync(key, cancellationToken);
            if (payload is null || payload.Length == 0)
                return default;

            var envelope = JsonSerializer.Deserialize<DistributedCacheEnvelope<T>>(
                payload,
                JsonOptions);
            if (envelope is null || envelope.Value is null)
                return default;

            var remaining = envelope.ExpiresAtUtc - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                await _distributedCache.RemoveAsync(key, cancellationToken);
                return default;
            }

            _memoryCache.Set(
                key,
                envelope.Value,
                GetLocalExpiration(remaining));

            return envelope.Value;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Distributed cache oxunarkən xəta baş verdi. Key: {CacheKey}",
                key);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan absoluteExpiration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (absoluteExpiration <= TimeSpan.Zero)
        {
            await RemoveAsync(key, cancellationToken);
            return;
        }

        var expiresAtUtc = DateTime.UtcNow.Add(absoluteExpiration);
        _memoryCache.Set(
            key,
            value,
            GetLocalExpiration(absoluteExpiration));

        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(
                new DistributedCacheEnvelope<T>
                {
                    ExpiresAtUtc = expiresAtUtc,
                    Value = value
                },
                JsonOptions);

            await _distributedCache.SetAsync(
                key,
                payload,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = new DateTimeOffset(expiresAtUtc)
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Distributed cache yazılarkən xəta baş verdi. Key: {CacheKey}",
                key);
        }
    }

    public async Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(key);

        try
        {
            await _distributedCache.RemoveAsync(key, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Distributed cache qeydi silinərkən xəta baş verdi. Key: {CacheKey}",
                key);
        }
    }

    private TimeSpan GetLocalExpiration(TimeSpan remaining)
    {
        var configured = TimeSpan.FromSeconds(
            Math.Max(1, _settings.LocalFallbackSeconds));
        return remaining < configured ? remaining : configured;
    }

}
