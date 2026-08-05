using Ludora.Application.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Ludora.API.HealthChecks;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IDistributedCache _cache;
    private readonly CacheSettings _settings;

    public RedisHealthCheck(
        IDistributedCache cache,
        IOptions<CacheSettings> options)
    {
        _cache = cache;
        _settings = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.RedisEnabled)
            return HealthCheckResult.Healthy("Redis cache is disabled.");

        var key = $"{_settings.InstanceName}health:{Guid.NewGuid():N}";

        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(5));

            await _cache.SetStringAsync(
                key,
                "ok",
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15)
                },
                timeoutSource.Token);

            var value = await _cache.GetStringAsync(key, timeoutSource.Token);
            await _cache.RemoveAsync(key, timeoutSource.Token);

            return value == "ok"
                ? HealthCheckResult.Healthy("Redis read/write probe succeeded.")
                : HealthCheckResult.Unhealthy("Redis returned an unexpected probe value.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException ||
                                           !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                "Redis is unavailable.",
                exception);
        }
    }
}
