namespace Ludora.Application.Settings;

public sealed class GroqSettings
{
    public const string SectionName = "GroqSettings";

    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";
    public string Model { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 25;
    public int MaxRetries { get; set; } = 2;
    public int CacheDurationHours { get; set; } = 12;
}
