using System.Net;

using Ludora.API.BackgroundServices;
using Ludora.Application.Settings;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

namespace Ludora.API.Configuration
{
    public static class DeploymentServiceRegistration
    {
        public static ForwardedHeadersSettings AddDeploymentConfiguration(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            services
                .AddOptions<JwtSettings>()
                .Bind(configuration.GetSection(JwtSettings.SectionName))
                .Validate(
                    settings => !string.IsNullOrWhiteSpace(settings.Key) &&
                                settings.Key.Length >= 32,
                    "JwtSettings:Key ən azı 32 simvol olmalıdır.")
                .Validate(
                    settings => !string.IsNullOrWhiteSpace(settings.Issuer),
                    "JwtSettings:Issuer boş ola bilməz.")
                .Validate(
                    settings => !string.IsNullOrWhiteSpace(settings.Audience),
                    "JwtSettings:Audience boş ola bilməz.")
                .Validate(
                    settings => settings.ExpiresInMinutes is >= 5 and <= 1440,
                    "JwtSettings:ExpiresInMinutes 5-1440 aralığında olmalıdır.")
                .ValidateOnStart();

            var configuredStorage = configuration
                .GetSection("FileStorageSettings")
                .Get<FileStorageSettings>() ?? new FileStorageSettings();
            var resolvedStorage = new FileStorageSettings
            {
                WebRootPath = DeploymentPathResolver.Resolve(
                    environment.ContentRootPath,
                    configuredStorage.WebRootPath),
                PrivateStoragePath = DeploymentPathResolver.Resolve(
                    environment.ContentRootPath,
                    configuredStorage.PrivateStoragePath)
            };

            if (PathsOverlap(
                    resolvedStorage.WebRootPath,
                    resolvedStorage.PrivateStoragePath))
            {
                throw new InvalidOperationException(
                    "Public və private storage qovluqları bir-birinin daxilində ola bilməz.");
            }

            services.AddSingleton(resolvedStorage);

            var configuredDataProtection = configuration
                .GetSection(DataProtectionSettings.SectionName)
                .Get<DataProtectionSettings>() ?? new DataProtectionSettings();
            var resolvedDataProtection = new DataProtectionSettings
            {
                ApplicationName = string.IsNullOrWhiteSpace(configuredDataProtection.ApplicationName)
                    ? "WellPlayd"
                    : configuredDataProtection.ApplicationName.Trim(),
                KeyRingPath = DeploymentPathResolver.Resolve(
                    environment.ContentRootPath,
                    configuredDataProtection.KeyRingPath)
            };

            if (IsInsidePath(
                    resolvedDataProtection.KeyRingPath,
                    resolvedStorage.WebRootPath))
            {
                throw new InvalidOperationException(
                    "Data Protection key ring public web root daxilində saxlanıla bilməz.");
            }

            Directory.CreateDirectory(resolvedDataProtection.KeyRingPath);
            services.AddSingleton(resolvedDataProtection);
            services
                .AddDataProtection()
                .SetApplicationName(resolvedDataProtection.ApplicationName)
                .PersistKeysToFileSystem(
                    new DirectoryInfo(resolvedDataProtection.KeyRingPath));
            services.AddHostedService<StorageInitializationHostedService>();

            var singleInstanceSettings = configuration
                .GetSection(SingleInstanceSettings.SectionName)
                .Get<SingleInstanceSettings>() ?? new SingleInstanceSettings();
            services.AddSingleton(singleInstanceSettings);
            services.AddHostedService<SingleInstanceLockHostedService>();

            var forwardedHeadersSettings = configuration
                .GetSection(ForwardedHeadersSettings.SectionName)
                .Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

            services
                .AddOptions<ForwardedHeadersSettings>()
                .Bind(configuration.GetSection(ForwardedHeadersSettings.SectionName))
                .Validate(
                    settings => settings.ForwardLimit is >= 1 and <= 5,
                    "ForwardedHeaders:ForwardLimit 1-5 aralığında olmalıdır.")
                .Validate(
                    settings => !settings.Enabled ||
                                settings.KnownProxies.Length > 0 ||
                                settings.KnownNetworks.Length > 0,
                    "ForwardedHeaders aktivdirsə trusted proxy və ya network verilməlidir.")
                .Validate(
                    settings => settings.KnownProxies.All(IsValidIpAddress),
                    "ForwardedHeaders:KnownProxies daxilində düzgün olmayan IP var.")
                .Validate(
                    settings => settings.KnownNetworks.All(IsValidNetwork),
                    "ForwardedHeaders:KnownNetworks CIDR formatında olmalıdır.")
                .ValidateOnStart();

            if (forwardedHeadersSettings.Enabled)
            {
                services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders =
                        ForwardedHeaders.XForwardedFor |
                        ForwardedHeaders.XForwardedProto |
                        ForwardedHeaders.XForwardedHost;
                    options.ForwardLimit = forwardedHeadersSettings.ForwardLimit;
                    options.RequireHeaderSymmetry = true;

                    options.KnownProxies.Clear();
                    options.KnownNetworks.Clear();

                    foreach (var proxy in forwardedHeadersSettings.KnownProxies)
                        options.KnownProxies.Add(IPAddress.Parse(proxy));

                    foreach (var network in forwardedHeadersSettings.KnownNetworks)
                    {
                        var parts = network.Split('/', 2, StringSplitOptions.TrimEntries);
                        options.KnownNetworks.Add(
                            new Microsoft.AspNetCore.HttpOverrides.IPNetwork(
                                IPAddress.Parse(parts[0]),
                                int.Parse(parts[1])));
                    }

                    foreach (var host in forwardedHeadersSettings.AllowedHosts
                                 .Where(host => !string.IsNullOrWhiteSpace(host)))
                    {
                        options.AllowedHosts.Add(host.Trim());
                    }
                });
            }

            return forwardedHeadersSettings;
        }

        private static bool IsInsidePath(string candidatePath, string parentPath)
        {
            var candidate = EnsureTrailingSeparator(Path.GetFullPath(candidatePath));
            var parent = EnsureTrailingSeparator(Path.GetFullPath(parentPath));
            return candidate.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathsOverlap(string firstPath, string secondPath)
        {
            var first = EnsureTrailingSeparator(Path.GetFullPath(firstPath));
            var second = EnsureTrailingSeparator(Path.GetFullPath(secondPath));

            return first.StartsWith(second, StringComparison.OrdinalIgnoreCase) ||
                   second.StartsWith(first, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTrailingSeparator(string path) =>
            path.EndsWith(Path.DirectorySeparatorChar)
                ? path
                : path + Path.DirectorySeparatorChar;

        private static bool IsValidIpAddress(string value) =>
            IPAddress.TryParse(value, out _);

        private static bool IsValidNetwork(string value)
        {
            var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !IPAddress.TryParse(parts[0], out var address) ||
                !int.TryParse(parts[1], out var prefixLength))
            {
                return false;
            }

            var maximumPrefixLength = address.AddressFamily ==
                                      System.Net.Sockets.AddressFamily.InterNetwork
                ? 32
                : 128;

            return prefixLength is >= 0 && prefixLength <= maximumPrefixLength;
        }
    }
}
