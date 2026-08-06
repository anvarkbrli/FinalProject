using Ludora.Application.Interfaces;
using Ludora.Application.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.FpsResearch
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddFpsResearchIntegration(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services
                .AddOptions<FpsResearchSettings>()
                .Bind(configuration.GetSection(FpsResearchSettings.SectionName))
                .PostConfigure(settings =>
                {
                    if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                        return;

                    settings.ApiKey =
                        configuration["GroqSettings:ApiKey"] ?? string.Empty;

                    if (bool.TryParse(
                            configuration["GroqSettings:Enabled"],
                            out var legacyEnabled))
                    {
                        settings.Enabled = legacyEnabled;
                    }
                })
                .Validate(
                    settings => !settings.Enabled ||
                                !string.IsNullOrWhiteSpace(settings.ApiKey),
                    "FPS research aktivdirsə ApiKey boş ola bilməz.")
                .Validate(
                    settings => !settings.Enabled ||
                                !string.IsNullOrWhiteSpace(settings.Model),
                    "FPS research aktivdirsə Model boş ola bilməz.")
                .Validate(
                    settings => IsAbsoluteHttpUrl(settings.BaseUrl),
                    "FpsResearch:BaseUrl düzgün URL deyil.")
                .Validate(
                    settings => !settings.Enabled ||
                                !string.IsNullOrWhiteSpace(
                                    settings.ModelVersion),
                    "FPS research aktivdirsə ModelVersion boş ola bilməz.")
                .Validate(
                    settings => settings.TimeoutSeconds is >= 10 and <= 180,
                    "FpsResearch:TimeoutSeconds 10-180 aralığında olmalıdır.")
                .Validate(
                    settings => settings.MaxRetries is >= 0 and <= 5,
                    "FpsResearch:MaxRetries 0-5 aralığında olmalıdır.")
                .Validate(
                    settings => settings.CacheDurationHours is >= 1 and <= 168,
                    "FpsResearch:CacheDurationHours 1-168 aralığında olmalıdır.")
                .ValidateOnStart();

            services.AddMemoryCache();
            services.AddScoped<IFpsEstimatorService, FpsEstimatorService>();

            services.AddHttpClient<IFpsResearchClient, GroqCompoundClient>(
                (serviceProvider, client) =>
                {
                    var settings = serviceProvider
                        .GetRequiredService<IOptions<FpsResearchSettings>>()
                        .Value;

                    client.BaseAddress =
                        new Uri(settings.BaseUrl.TrimEnd('/') + "/");
                    client.Timeout =
                        TimeSpan.FromSeconds(settings.TimeoutSeconds);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(
                        "Ludora-FpsBenchmark/2.0");
                });

            services.AddScoped<
                IHomeInterestRecommendationService,
                HomeInterestRecommendationService>();

            return services;
        }

        private static bool IsAbsoluteHttpUrl(string? value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttps ||
                    uri.Scheme == Uri.UriSchemeHttp);
        }
    }
}
