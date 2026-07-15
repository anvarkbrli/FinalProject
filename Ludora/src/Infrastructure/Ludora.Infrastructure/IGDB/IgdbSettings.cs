namespace Ludora.Infrastructure.IGDB;

public sealed class IgdbSettings
{
    public const string SectionName = "Igdb";

    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api.igdb.com/v4/";
    public string TokenUrl { get; set; } = "https://id.twitch.tv/oauth2/token";
    public int RequestTimeoutSeconds { get; set; } = 30;
}
