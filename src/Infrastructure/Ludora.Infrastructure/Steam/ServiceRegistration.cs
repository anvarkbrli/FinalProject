using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.Steam;

public static class ServiceRegistration
{
    public static IServiceCollection AddSteamIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<SteamSettings>()
            .Bind(configuration.GetSection(SteamSettings.SectionName))
            .Validate(
                settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.ApiKey),
                "Steam aktivdirsə ApiKey boş ola bilməz.")
            .Validate(settings => IsAbsoluteUrl(settings.WebApiBaseUrl), "Steam:WebApiBaseUrl düzgün URL deyil.")
            .Validate(settings => IsAbsoluteUrl(settings.StorefrontBaseUrl), "Steam:StorefrontBaseUrl düzgün URL deyil.")
            .Validate(settings => IsAbsoluteUrl(settings.OpenIdEndpoint), "Steam:OpenIdEndpoint düzgün URL deyil.")
            .Validate(settings => IsAbsoluteUrl(settings.OpenIdRealm), "Steam:OpenIdRealm düzgün URL deyil.")
            .Validate(settings => IsAbsoluteUrl(settings.OpenIdCallbackUrl), "Steam:OpenIdCallbackUrl düzgün URL deyil.")
            .Validate(settings => IsAbsoluteUrl(settings.FrontendSettingsUrl), "Steam:FrontendSettingsUrl düzgün URL deyil.")
            .Validate(settings => IsFrontendOrigin(settings.FrontendOrigin), "Steam:FrontendOrigin yalnız origin formatında olmalıdır.")
            .Validate(
                settings => IsCallbackInsideRealm(settings.OpenIdCallbackUrl, settings.OpenIdRealm),
                "Steam:OpenIdCallbackUrl Steam:OpenIdRealm daxilində olmalıdır.")
            .Validate(settings => settings.RequestTimeoutSeconds is >= 5 and <= 120, "Steam:RequestTimeoutSeconds 5-120 aralığında olmalıdır.")
            .Validate(settings => settings.RequestsPerMinute is >= 1 and <= 600, "Steam:RequestsPerMinute 1-600 aralığında olmalıdır.")
            .Validate(settings => settings.MaxConcurrentRequests is >= 1 and <= 20, "Steam:MaxConcurrentRequests 1-20 aralığında olmalıdır.")
            .Validate(settings => settings.RetryCount is >= 0 and <= 5, "Steam:RetryCount 0-5 aralığında olmalıdır.")
            .Validate(settings => settings.LinkStateLifetimeMinutes is >= 2 and <= 30, "Steam:LinkStateLifetimeMinutes 2-30 aralığında olmalıdır.")
            .ValidateOnStart();

        services.AddMemoryCache();
        services.AddSingleton<SteamRequestThrottle>();

        services.AddHttpClient<ISteamApiClient, SteamApiClient>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<SteamSettings>>().Value;
            client.BaseAddress = new Uri(settings.WebApiBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WellPlayd-SteamIntegration/1.0");
        });

        services.AddHttpClient<ISteamStoreClient, SteamStoreClient>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<SteamSettings>>().Value;
            client.BaseAddress = new Uri(settings.StorefrontBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WellPlayd-SteamIntegration/1.0");
        });

        services.AddHttpClient<ISteamOpenIdClient, SteamOpenIdClient>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<SteamSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WellPlayd-SteamIntegration/1.0");
        });

        return services;
    }

    private static bool IsAbsoluteUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    private static bool IsFrontendOrigin(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
               uri.AbsolutePath == "/" &&
               string.IsNullOrEmpty(uri.Query) &&
               string.IsNullOrEmpty(uri.Fragment);
    }

    private static bool IsCallbackInsideRealm(string? callbackValue, string? realmValue)
    {
        if (!Uri.TryCreate(callbackValue, UriKind.Absolute, out var callback) ||
            !Uri.TryCreate(realmValue, UriKind.Absolute, out var realm))
        {
            return false;
        }

        if (!string.Equals(callback.Scheme, realm.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(callback.Host, realm.Host, StringComparison.OrdinalIgnoreCase) ||
            callback.Port != realm.Port)
        {
            return false;
        }

        var realmPath = realm.AbsolutePath.TrimEnd('/') + "/";
        var callbackPath = callback.AbsolutePath.TrimEnd('/') + "/";
        return callbackPath.StartsWith(realmPath, StringComparison.OrdinalIgnoreCase);
    }
}
