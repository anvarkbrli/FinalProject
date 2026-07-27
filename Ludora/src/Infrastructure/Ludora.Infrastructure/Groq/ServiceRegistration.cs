using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.Groq;

public static class ServiceRegistration
{
    public static IServiceCollection AddGroqIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<GroqSettings>()
            .Bind(configuration.GetSection(GroqSettings.SectionName))
            .Validate(
                settings => !settings.Enabled ||
                            !string.IsNullOrWhiteSpace(settings.ApiKey),
                "Groq aktivdirsə ApiKey boş ola bilməz.")
            .Validate(
                settings => !settings.Enabled ||
                            !string.IsNullOrWhiteSpace(settings.Model),
                "Groq aktivdirsə Model boş ola bilməz.")
            .Validate(
                settings => IsAbsoluteHttpUrl(settings.BaseUrl),
                "GroqSettings:BaseUrl düzgün URL deyil.")
            .Validate(
                settings => settings.TimeoutSeconds is >= 5 and <= 120,
                "GroqSettings:TimeoutSeconds 5-120 aralığında olmalıdır.")
            .Validate(
                settings => settings.MaxRetries is >= 0 and <= 5,
                "GroqSettings:MaxRetries 0-5 aralığında olmalıdır.")
            .Validate(
                settings => settings.CacheDurationHours is >= 1 and <= 168,
                "GroqSettings:CacheDurationHours 1-168 aralığında olmalıdır.")
            .ValidateOnStart();

        services.AddMemoryCache();
        services.AddScoped<IFpsEstimatorService, FpsEstimatorService>();

        services.AddHttpClient<IGroqClient, GroqClient>(
            (serviceProvider, client) =>
            {
                var settings = serviceProvider
                    .GetRequiredService<IOptions<GroqSettings>>()
                    .Value;

                client.BaseAddress =
                    new Uri(settings.BaseUrl.TrimEnd('/') + "/");
                client.Timeout =
                    TimeSpan.FromSeconds(settings.TimeoutSeconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Ludora-FpsEstimator/1.0");
            });

        return services;
    }

    private static bool IsAbsoluteHttpUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttps ||
                uri.Scheme == Uri.UriSchemeHttp);
    }
}
