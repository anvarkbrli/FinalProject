using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ludora.Infrastructure.Caching;

public static class ServiceRegistration
{
    public static IServiceCollection AddCachingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration
            .GetSection(CacheSettings.SectionName)
            .Get<CacheSettings>() ?? new CacheSettings();

        services
            .AddOptions<CacheSettings>()
            .Bind(configuration.GetSection(CacheSettings.SectionName))
            .Validate(
                options => !options.RedisEnabled ||
                           !string.IsNullOrWhiteSpace(options.RedisConnectionString),
                "Cache:RedisConnectionString Redis aktiv olduqda boş ola bilməz.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.InstanceName),
                "Cache:InstanceName boş ola bilməz.")
            .Validate(
                options => options.LocalFallbackSeconds is >= 1 and <= 3600,
                "Cache:LocalFallbackSeconds 1-3600 aralığında olmalıdır.")
            .ValidateOnStart();

        services.AddMemoryCache();

        if (settings.RedisEnabled)
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = settings.RedisConnectionString;
            });
            services.AddSingleton<IAppCache, HybridAppCache>();
        }
        else
        {
            services.AddSingleton<IAppCache, MemoryAppCache>();
        }

        return services;
    }
}
