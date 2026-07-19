using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ludora.Infrastructure.Email;

public static class ServiceRegistration
{
    public static IServiceCollection AddEmailInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<EmailSettings>()
            .Bind(configuration.GetSection(EmailSettings.SectionName))
            .Validate(
                settings => !settings.Enabled ||
                            !string.IsNullOrWhiteSpace(settings.Host),
                "EmailSettings:Host boş ola bilməz.")
            .Validate(
                settings => !settings.Enabled ||
                            settings.Port is > 0 and <= 65535,
                "EmailSettings:Port düzgün deyil.")
            .Validate(
                settings => !settings.Enabled ||
                            !string.IsNullOrWhiteSpace(settings.FromEmail),
                "EmailSettings:FromEmail boş ola bilməz.")
            .Validate(
                settings => !settings.Enabled ||
                            Uri.TryCreate(
                                settings.PublicApiBaseUrl,
                                UriKind.Absolute,
                                out _),
                "EmailSettings:PublicApiBaseUrl düzgün URL deyil.")
            .ValidateOnStart();

        services.AddScoped<IEmailService, SmtpEmailService>();

        return services;
    }
}
