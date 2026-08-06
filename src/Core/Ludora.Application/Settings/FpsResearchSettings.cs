namespace Ludora.Application.Settings
{
    public sealed class FpsResearchSettings
    {
        public const string SectionName = "FpsResearch";

        public bool Enabled { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } =
            "https://api.groq.com/openai/v1";
        public string Model { get; set; } = "groq/compound";
        public string ModelVersion { get; set; } = "2025-07-23";
        public int TimeoutSeconds { get; set; } = 60;
        public int MaxRetries { get; set; } = 2;
        public int CacheDurationHours { get; set; } = 24;
    }
}
