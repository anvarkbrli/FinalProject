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
        public string ModelVersion { get; set; } = "latest";
        public int TimeoutSeconds { get; set; } = 90;
        public int MaxRetries { get; set; } = 1;
        public int CacheDurationHours { get; set; } = 24;
    }
}
