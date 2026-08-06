namespace Ludora.Application.Settings
{
    public sealed class SteamSettings
    {
        public const string SectionName = "Steam";

        public bool Enabled { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public string WebApiBaseUrl { get; set; } = "https://api.steampowered.com/";
        public string StorefrontBaseUrl { get; set; } = "https://store.steampowered.com/api/";
        public string OpenIdEndpoint { get; set; } = "https://steamcommunity.com/openid/login";
        public string OpenIdRealm { get; set; } = "https://localhost:7286/";
        public string OpenIdCallbackUrl { get; set; } = "https://localhost:7286/api/settings/export/steam/callback";
        public string FrontendSettingsUrl { get; set; } = "http://localhost:5173/settings?section=export";
        public string FrontendOrigin { get; set; } = "http://localhost:5173";
        public string CountryCode { get; set; } = "AZ";
        public string Language { get; set; } = "english";
        public int RequestTimeoutSeconds { get; set; } = 30;
        public int RequestsPerMinute { get; set; } = 90;
        public int MaxConcurrentRequests { get; set; } = 4;
        public int RetryCount { get; set; } = 3;
        public int LinkStateLifetimeMinutes { get; set; } = 10;
        public int ProfileCacheSeconds { get; set; } = 15;
        public int AchievementSchemaCacheMinutes { get; set; } = 360;
        public int ComingSoonCacheMinutes { get; set; } = 30;
    }
}
