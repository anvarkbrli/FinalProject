namespace Ludora.Infrastructure.Translation
{
    public sealed class TranslationSettings
    {
        public const string SectionName = "Translation";
        public string Provider { get; init; } = string.Empty;
        public string ApiKey { get; init; } = string.Empty;
        public string Endpoint { get; init; } = "https://api.cognitive.microsofttranslator.com";
        public string Region { get; init; } = string.Empty;
        public int RequestTimeoutSeconds { get; init; } = 15;
    }
}
