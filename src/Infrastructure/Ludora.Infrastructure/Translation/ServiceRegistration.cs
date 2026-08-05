using Ludora.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.Translation;

public static class ServiceRegistration
{
    public static IServiceCollection AddTranslationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<TranslationSettings>()
            .Bind(configuration.GetSection(TranslationSettings.SectionName))
            .Validate(
                settings => settings.RequestTimeoutSeconds is >= 5 and <= 120,
                "Translation:RequestTimeoutSeconds 5-120 aralığında olmalıdır.")
            .ValidateOnStart();

        services.AddHttpClient<ITranslationProvider, AzureTranslationProvider>(
            (serviceProvider, client) =>
            {
                var settings = serviceProvider
                    .GetRequiredService<IOptions<TranslationSettings>>()
                    .Value;
                client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
            });
        return services;
    }
}
