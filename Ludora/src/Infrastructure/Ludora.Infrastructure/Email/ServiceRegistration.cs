using System.ComponentModel.DataAnnotations;
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
                            IsValidEmail(settings.FromEmail),
                "EmailSettings:FromEmail düzgün email ünvanı olmalıdır.")
            .Validate(
                settings => !settings.Enabled ||
                            HasConsistentCredentials(settings),
                "EmailSettings:UserName və EmailSettings:Password birlikdə verilməlidir.")
            .Validate(
                settings => !settings.Enabled ||
                            IsHttpUrl(settings.PublicApiBaseUrl),
                "EmailSettings:PublicApiBaseUrl düzgün http/https URL olmalıdır.")
            .Validate(
                settings => !settings.Enabled ||
                            IsHttpUrl(settings.PublicFrontendBaseUrl),
                "EmailSettings:PublicFrontendBaseUrl düzgün http/https URL olmalıdır.")
            .Validate(
                settings => settings.MaxRetryAttempts is >= 1 and <= 5,
                "EmailSettings:MaxRetryAttempts 1-5 aralığında olmalıdır.")
            .Validate(
                settings => settings.RetryDelaySeconds is >= 0 and <= 30,
                "EmailSettings:RetryDelaySeconds 0-30 aralığında olmalıdır.")
            .Validate(
                settings => settings.SendTimeoutSeconds is >= 5 and <= 120,
                "EmailSettings:SendTimeoutSeconds 5-120 aralığında olmalıdır.")
            .ValidateOnStart();

        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddHostedService<EmailConfigurationLogger>();

        return services;
    }

    private static bool HasConsistentCredentials(EmailSettings settings)
    {
        var hasUserName = !string.IsNullOrWhiteSpace(settings.UserName);
        var hasPassword = !string.IsNullOrWhiteSpace(settings.Password);
        return hasUserName == hasPassword;
    }

    private static bool IsValidEmail(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               new EmailAddressAttribute().IsValid(value);
    }

    private static bool IsHttpUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp ||
                uri.Scheme == Uri.UriSchemeHttps);
    }
}
