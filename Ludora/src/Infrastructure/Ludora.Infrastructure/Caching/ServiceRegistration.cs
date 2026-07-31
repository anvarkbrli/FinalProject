using Ludora.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Ludora.Infrastructure.Caching;

public static class ServiceRegistration
{
    public static IServiceCollection AddCachingInfrastructure(
        this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IAppCache, MemoryAppCache>();
        return services;
    }
}
