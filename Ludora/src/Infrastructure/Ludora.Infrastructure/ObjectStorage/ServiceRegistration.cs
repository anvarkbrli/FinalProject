using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ludora.Infrastructure.ObjectStorage;

public static class ServiceRegistration
{
    public static IServiceCollection AddObjectStorageInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration
            .GetSection(ObjectStorageSettings.SectionName)
            .Get<ObjectStorageSettings>() ?? new ObjectStorageSettings();

        ValidateCommonSettings(settings);
        services.AddSingleton(settings);

        if (string.Equals(settings.Provider, "S3", StringComparison.OrdinalIgnoreCase))
        {
            ValidateS3Settings(settings);
            services.AddSingleton<IAmazonS3>(_ => CreateClient(settings));
            services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        }
        else if (string.Equals(settings.Provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IObjectStorage, LocalObjectStorage>();
        }
        else
        {
            throw new InvalidOperationException(
                "ObjectStorage:Provider yalnız Local və ya S3 ola bilər.");
        }

        return services;
    }

    private static IAmazonS3 CreateClient(ObjectStorageSettings settings)
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = settings.ForcePathStyle,
            UseHttp = settings.UseHttp
        };

        if (!string.IsNullOrWhiteSpace(settings.ServiceUrl))
        {
            config.ServiceURL = settings.ServiceUrl.TrimEnd('/');
            config.AuthenticationRegion = settings.Region;
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region);
        }

        var hasExplicitCredentials =
            !string.IsNullOrWhiteSpace(settings.AccessKey) &&
            !string.IsNullOrWhiteSpace(settings.SecretKey);

        return hasExplicitCredentials
            ? new AmazonS3Client(
                new BasicAWSCredentials(settings.AccessKey, settings.SecretKey),
                config)
            : new AmazonS3Client(config);
    }

    private static void ValidateCommonSettings(ObjectStorageSettings settings)
    {
        var publicPrefix = NormalizePrefix(settings.PublicPrefix);
        var privatePrefix = NormalizePrefix(settings.PrivatePrefix);

        if (string.IsNullOrWhiteSpace(publicPrefix) ||
            string.IsNullOrWhiteSpace(privatePrefix))
        {
            throw new InvalidOperationException(
                "ObjectStorage public və private prefix-ləri boş ola bilməz.");
        }

        if (string.Equals(publicPrefix, privatePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "ObjectStorage public və private prefix-ləri fərqli olmalıdır.");
        }
    }

    private static void ValidateS3Settings(ObjectStorageSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.BucketName) ||
            string.IsNullOrWhiteSpace(settings.PublicBaseUrl))
        {
            throw new InvalidOperationException(
                "S3 object storage üçün BucketName və PublicBaseUrl verilməlidir.");
        }

        var hasAccessKey = !string.IsNullOrWhiteSpace(settings.AccessKey);
        var hasSecretKey = !string.IsNullOrWhiteSpace(settings.SecretKey);
        if (hasAccessKey != hasSecretKey)
        {
            throw new InvalidOperationException(
                "ObjectStorage:AccessKey və SecretKey birlikdə verilməlidir.");
        }

        if (!IsHttpUrl(settings.PublicBaseUrl))
        {
            throw new InvalidOperationException(
                "ObjectStorage:PublicBaseUrl düzgün http/https URL olmalıdır.");
        }

        if (!string.IsNullOrWhiteSpace(settings.ServiceUrl) &&
            !IsHttpUrl(settings.ServiceUrl))
        {
            throw new InvalidOperationException(
                "ObjectStorage:ServiceUrl düzgün http/https URL olmalıdır.");
        }

        if (string.IsNullOrWhiteSpace(settings.ServiceUrl) &&
            string.IsNullOrWhiteSpace(settings.Region))
        {
            throw new InvalidOperationException(
                "AWS S3 üçün ObjectStorage:Region boş ola bilməz.");
        }
    }

    private static string NormalizePrefix(string? value) =>
        (value ?? string.Empty).Replace('\\', '/').Trim().Trim('/');

    private static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp ||
         uri.Scheme == Uri.UriSchemeHttps);
}
