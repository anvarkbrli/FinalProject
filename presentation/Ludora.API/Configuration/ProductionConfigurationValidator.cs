using Ludora.Application.Settings;
using Ludora.Infrastructure.IGDB;
using Ludora.Infrastructure.Translation;

namespace Ludora.API.Configuration;

public static class ProductionConfigurationValidator
{
    private static readonly string[] PlaceholderFragments =
    [
        "change-me",
        "changeme",
        "replace-me",
        "replace_me",
        "your-secret",
        "example.com",
        ".example",
        "<",
        ">"
    ];

    public static void Validate(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!environment.IsProduction())
            return;

        var errors = new List<string>();

        ValidateDatabase(configuration, errors);
        ValidateJwt(configuration, errors);
        ValidateCors(configuration, errors);
        ValidateAllowedHosts(configuration, errors);
        ValidateStorage(configuration, errors);
        ValidateDataProtection(configuration, errors);
        ValidateCache(configuration, errors);
        ValidateMalwareScanning(configuration, errors);
        ValidateForwardedHeaders(configuration, errors);
        ValidateEmail(configuration, errors);
        ValidateGoogleAuthentication(configuration, errors);
        ValidateOptionalIntegrations(configuration, errors);

        if (errors.Count == 0)
            return;

        throw new InvalidOperationException(
            "Production konfiqurasiyası etibarlı deyil:" +
            Environment.NewLine +
            string.Join(
                Environment.NewLine,
                errors.Select((error, index) => $"  {index + 1}. {error}")));
    }

    private static void ValidateDatabase(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        var connectionString = configuration.GetConnectionString("default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            errors.Add("ConnectionStrings:default boş ola bilməz.");
            return;
        }

        if (connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                "Production mühitində LocalDB istifadə edilə bilməz. SQL Server bağlantısını environment variable ilə verin.");
        }

        if (ContainsPlaceholder(connectionString))
            errors.Add("ConnectionStrings:default placeholder dəyər saxlayır.");
    }

    private static void ValidateJwt(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        var settings = configuration
            .GetSection(JwtSettings.SectionName)
            .Get<JwtSettings>() ?? new JwtSettings();

        if (string.IsNullOrWhiteSpace(settings.Key) || settings.Key.Length < 32)
            errors.Add("JwtSettings:Key ən azı 32 simvol olmalıdır.");
        else if (ContainsPlaceholder(settings.Key))
            errors.Add("JwtSettings:Key real, təsadüfi secret ilə əvəz edilməlidir.");

        if (string.IsNullOrWhiteSpace(settings.Issuer))
            errors.Add("JwtSettings:Issuer boş ola bilməz.");
        if (string.IsNullOrWhiteSpace(settings.Audience))
            errors.Add("JwtSettings:Audience boş ola bilməz.");
        if (settings.ExpiresInMinutes is < 5 or > 1440)
            errors.Add("JwtSettings:ExpiresInMinutes 5-1440 aralığında olmalıdır.");
    }

    private static void ValidateCors(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        var origins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        if (origins.Length == 0)
        {
            errors.Add("Cors:AllowedOrigins ən azı bir frontend origin saxlamalıdır.");
            return;
        }

        foreach (var origin in origins)
        {
            if (!TryParseOrigin(origin, out var uri))
            {
                errors.Add($"Cors:AllowedOrigins daxilində düzgün origin deyil: '{origin}'.");
                continue;
            }

            if (ContainsPlaceholder(origin))
                errors.Add($"Production CORS origin placeholder ola bilməz: '{origin}'.");

            if (uri!.Scheme != Uri.UriSchemeHttps)
                errors.Add($"Production CORS origin HTTPS olmalıdır: '{origin}'.");

            if (uri.IsLoopback ||
                uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Production CORS origin localhost ola bilməz: '{origin}'.");
            }
        }
    }

    private static void ValidateAllowedHosts(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        var allowedHosts = configuration["AllowedHosts"]?.Trim();
        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts == "*")
        {
            errors.Add(
                "AllowedHosts production mühitində '*' ola bilməz; real host adlarını ';' ilə ayıraraq verin.");
        }
        else if (ContainsPlaceholder(allowedHosts))
        {
            errors.Add("AllowedHosts placeholder host adları saxlaya bilməz.");
        }
    }

    private static void ValidateStorage(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        var settings = configuration
            .GetSection("FileStorageSettings")
            .Get<FileStorageSettings>() ?? new FileStorageSettings();

        if (string.IsNullOrWhiteSpace(settings.WebRootPath))
            errors.Add("FileStorageSettings:WebRootPath boş ola bilməz.");
        else if (!Path.IsPathRooted(settings.WebRootPath))
            errors.Add("Production mühitində FileStorageSettings:WebRootPath absolute path olmalıdır.");

        if (string.IsNullOrWhiteSpace(settings.PrivateStoragePath))
            errors.Add("FileStorageSettings:PrivateStoragePath boş ola bilməz.");
        else if (!Path.IsPathRooted(settings.PrivateStoragePath))
            errors.Add("Production mühitində FileStorageSettings:PrivateStoragePath absolute path olmalıdır.");

        if (!string.IsNullOrWhiteSpace(settings.WebRootPath) &&
            !string.IsNullOrWhiteSpace(settings.PrivateStoragePath))
        {
            var webRoot = Path.GetFullPath(settings.WebRootPath)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var privateRoot = Path.GetFullPath(settings.PrivateStoragePath)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (privateRoot.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase) ||
                webRoot.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    "Public və private storage qovluqları bir-birinin daxilində ola bilməz.");
            }
        }
    }


    private static void ValidateDataProtection(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        var settings = configuration
            .GetSection(DataProtectionSettings.SectionName)
            .Get<DataProtectionSettings>() ?? new DataProtectionSettings();

        if (string.IsNullOrWhiteSpace(settings.ApplicationName))
            errors.Add("DataProtection:ApplicationName boş ola bilməz.");

        if (string.IsNullOrWhiteSpace(settings.KeyRingPath))
        {
            errors.Add("DataProtection:KeyRingPath boş ola bilməz.");
            return;
        }

        if (!Path.IsPathRooted(settings.KeyRingPath))
            errors.Add("Production mühitində DataProtection:KeyRingPath absolute path olmalıdır.");

        var storage = configuration
            .GetSection("FileStorageSettings")
            .Get<FileStorageSettings>() ?? new FileStorageSettings();
        if (!string.IsNullOrWhiteSpace(storage.WebRootPath))
        {
            var publicRoot = Path.GetFullPath(storage.WebRootPath)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var keyRing = Path.GetFullPath(settings.KeyRingPath)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (keyRing.StartsWith(publicRoot, StringComparison.OrdinalIgnoreCase))
                errors.Add("Data Protection key ring public web root daxilində saxlanıla bilməz.");
        }
    }

    private static void ValidateCache(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        var settings = configuration
            .GetSection(CacheSettings.SectionName)
            .Get<CacheSettings>() ?? new CacheSettings();

        if (settings.RedisEnabled &&
            string.IsNullOrWhiteSpace(settings.RedisConnectionString))
        {
            errors.Add("Cache:RedisConnectionString Redis aktiv olduqda boş ola bilməz.");
        }

        if (settings.RedisEnabled && ContainsPlaceholder(settings.RedisConnectionString))
            errors.Add("Cache:RedisConnectionString placeholder dəyər saxlaya bilməz.");

        if (string.IsNullOrWhiteSpace(settings.InstanceName))
            errors.Add("Cache:InstanceName boş ola bilməz.");
    }

    private static void ValidateMalwareScanning(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        var settings = configuration
            .GetSection("MalwareScanning")
            .Get<MalwareScanningSettings>() ?? new MalwareScanningSettings();

        if (!settings.Enabled)
            errors.Add("Production mühitində MalwareScanning:Enabled true olmalıdır.");
        if (!settings.FailClosed)
            errors.Add("Production mühitində MalwareScanning:FailClosed true olmalıdır.");
        if (string.IsNullOrWhiteSpace(settings.Host))
            errors.Add("MalwareScanning:Host boş ola bilməz.");
        if (settings.Port is < 1 or > 65535)
            errors.Add("MalwareScanning:Port 1-65535 aralığında olmalıdır.");
        if (settings.TimeoutSeconds is < 1 or > 300)
            errors.Add("MalwareScanning:TimeoutSeconds 1-300 aralığında olmalıdır.");
        if (settings.ChunkSizeBytes is < 1024 or > 1_048_576)
            errors.Add("MalwareScanning:ChunkSizeBytes 1024-1048576 aralığında olmalıdır.");
    }

    private static void ValidateForwardedHeaders(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        var settings = configuration
            .GetSection(ForwardedHeadersSettings.SectionName)
            .Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

        if (!settings.Enabled)
        {
            errors.Add(
                "Production reverse proxy arxasında işləyəcəksə ForwardedHeaders:Enabled true olmalıdır.");
            return;
        }

        if (settings.ForwardLimit is < 1 or > 5)
            errors.Add("ForwardedHeaders:ForwardLimit 1-5 aralığında olmalıdır.");

        if (settings.KnownProxies.Length == 0 && settings.KnownNetworks.Length == 0)
        {
            errors.Add(
                "ForwardedHeaders üçün ən azı bir KnownProxies və ya KnownNetworks dəyəri verilməlidir.");
        }

        if (settings.AllowedHosts.Length == 0)
            errors.Add("ForwardedHeaders:AllowedHosts ən azı bir real host saxlamalıdır.");
        else if (settings.AllowedHosts.Any(ContainsPlaceholder))
            errors.Add("ForwardedHeaders:AllowedHosts placeholder host saxlaya bilməz.");
    }

    private static void ValidateEmail(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        var settings = configuration
            .GetSection(EmailSettings.SectionName)
            .Get<EmailSettings>() ?? new EmailSettings();

        if (!settings.Enabled)
        {
            errors.Add(
                "EmailSettings:Enabled true olmalıdır; qeydiyyat email təsdiqi tələb edir.");
            return;
        }

        ValidateProductionUrl(
            settings.PublicApiBaseUrl,
            "EmailSettings:PublicApiBaseUrl",
            errors);
        ValidateProductionUrl(
            settings.PublicFrontendBaseUrl,
            "EmailSettings:PublicFrontendBaseUrl",
            errors);
    }

    private static void ValidateGoogleAuthentication(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        var settings = configuration
            .GetSection(GoogleAuthenticationSettings.SectionName)
            .Get<GoogleAuthenticationSettings>() ?? new GoogleAuthenticationSettings();

        if (!settings.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(settings.ClientId) || ContainsPlaceholder(settings.ClientId))
            errors.Add("Authentication:Google:ClientId real Google OAuth client ID olmalıdır.");
        if (string.IsNullOrWhiteSpace(settings.ClientSecret) || ContainsPlaceholder(settings.ClientSecret))
            errors.Add("Authentication:Google:ClientSecret real secret olmalıdır.");
        if (string.IsNullOrWhiteSpace(settings.CallbackPath) ||
            !settings.CallbackPath.StartsWith("/", StringComparison.Ordinal) ||
            settings.CallbackPath.StartsWith("//", StringComparison.Ordinal))
            errors.Add("Authentication:Google:CallbackPath düzgün relative path olmalıdır.");
        if (!Uri.TryCreate(settings.FrontendCallbackUrl, UriKind.Absolute, out var callbackUri) ||
            callbackUri.Scheme != Uri.UriSchemeHttps ||
            callbackUri.IsLoopback ||
            ContainsPlaceholder(settings.FrontendCallbackUrl))
            errors.Add("Authentication:Google:FrontendCallbackUrl production-da real HTTPS URL olmalıdır.");
        if (settings.TicketLifetimeMinutes is < 3 or > 30)
            errors.Add("Authentication:Google:TicketLifetimeMinutes 3-30 aralığında olmalıdır.");
    }

    private static void ValidateOptionalIntegrations(
        IConfiguration configuration,
        ICollection<string> errors)
    {
        var steam = configuration
            .GetSection(SteamSettings.SectionName)
            .Get<SteamSettings>() ?? new SteamSettings();
        if (steam.Enabled)
        {
            ValidateProductionUrl(steam.OpenIdRealm, "Steam:OpenIdRealm", errors);
            ValidateProductionUrl(
                steam.OpenIdCallbackUrl,
                "Steam:OpenIdCallbackUrl",
                errors);
            ValidateProductionUrl(
                steam.FrontendSettingsUrl,
                "Steam:FrontendSettingsUrl",
                errors);
            ValidateProductionUrl(
                steam.FrontendOrigin,
                "Steam:FrontendOrigin",
                errors,
                requireOriginOnly: true);
        }

        var igdb = configuration
            .GetSection(IgdbSettings.SectionName)
            .Get<IgdbSettings>() ?? new IgdbSettings();
        if (igdb.Enabled &&
            (ContainsPlaceholder(igdb.ClientId) || ContainsPlaceholder(igdb.ClientSecret)))
        {
            errors.Add("Igdb credential-ları placeholder dəyər ola bilməz.");
        }

        var fpsResearch = configuration
            .GetSection(FpsResearchSettings.SectionName)
            .Get<FpsResearchSettings>() ?? new FpsResearchSettings();
        if (fpsResearch.Enabled && ContainsPlaceholder(fpsResearch.ApiKey))
            errors.Add("FpsResearch:ApiKey placeholder dəyər ola bilməz.");

        var translation = configuration
            .GetSection(TranslationSettings.SectionName)
            .Get<TranslationSettings>() ?? new TranslationSettings();
        if (!string.IsNullOrWhiteSpace(translation.Provider) &&
            ContainsPlaceholder(translation.ApiKey))
        {
            errors.Add("Translation:ApiKey placeholder dəyər ola bilməz.");
        }
    }

    private static bool TryParseOrigin(string? value, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp &&
             parsed.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        if (parsed.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(parsed.Query) ||
            !string.IsNullOrEmpty(parsed.Fragment) ||
            !string.IsNullOrEmpty(parsed.UserInfo))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    private static void ValidateProductionUrl(
        string? value,
        string key,
        ICollection<string> errors,
        bool requireOriginOnly = false)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add($"{key} production mühitində düzgün HTTPS URL olmalıdır.");
            return;
        }

        if (ContainsPlaceholder(value))
            errors.Add($"{key} placeholder URL ola bilməz.");

        if (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            errors.Add($"{key} localhost ola bilməz.");

        if (requireOriginOnly &&
            (uri.AbsolutePath != "/" ||
             !string.IsNullOrEmpty(uri.Query) ||
             !string.IsNullOrEmpty(uri.Fragment)))
        {
            errors.Add($"{key} yalnız origin formatında olmalıdır.");
        }
    }

    private static bool ContainsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return PlaceholderFragments.Any(fragment =>
            value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
