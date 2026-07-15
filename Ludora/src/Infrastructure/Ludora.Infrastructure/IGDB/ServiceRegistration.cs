using Ludora.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.IGDB;

public static class ServiceRegistration
{
    public static IServiceCollection AddIgdbIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<IgdbSettings>()
            .Bind(configuration.GetSection(IgdbSettings.SectionName))
            .Validate(
                settings => !settings.Enabled ||
                            !string.IsNullOrWhiteSpace(settings.ClientId),
                "Igdb aktivdirsə ClientId boş ola bilməz.")
            .Validate(
                settings => !settings.Enabled ||
                            !string.IsNullOrWhiteSpace(settings.ClientSecret),
                "Igdb aktivdirsə ClientSecret boş ola bilməz.")
            .Validate(
                settings => Uri.TryCreate(settings.ApiBaseUrl, UriKind.Absolute, out _),
                "Igdb:ApiBaseUrl düzgün URL deyil.")
            .Validate(
                settings => Uri.TryCreate(settings.TokenUrl, UriKind.Absolute, out _),
                "Igdb:TokenUrl düzgün URL deyil.")
            .Validate(
                settings => settings.RequestTimeoutSeconds is >= 5 and <= 120,
                "Igdb:RequestTimeoutSeconds 5-120 aralığında olmalıdır.")
            .ValidateOnStart();

        services.AddHttpClient("IgdbToken", (serviceProvider, client) =>
        {
            var settings = serviceProvider
                .GetRequiredService<IOptions<IgdbSettings>>()
                .Value;

            client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
        });

        services.AddSingleton<IgdbTokenProvider>();
        services.AddSingleton<IgdbRequestThrottle>();

        services.AddHttpClient<IIgdbClient, IgdbClient>((serviceProvider, client) =>
        {
            var settings = serviceProvider
                .GetRequiredService<IOptions<IgdbSettings>>()
                .Value;

            client.BaseAddress = new Uri(settings.ApiBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
        });

        return services;
    }
}
