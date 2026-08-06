namespace Ludora.Application.Interfaces
{
    public interface IFpsResearchClient
    {
        bool IsEnabled { get; }

        Task<FpsResearchCompletion> ResearchAsync(
            string systemPrompt,
            string benchmarkSearchPrompt,
            CancellationToken cancellationToken = default,
            string? modelOverride = null,
            string? modelVersionOverride = null);
    }

    public sealed record FpsResearchCompletion
    {
        public string Content { get; init; } = string.Empty;
        public IReadOnlyCollection<FpsResearchSource> Sources { get; init; } = [];
    }

    public sealed record FpsResearchSource
    {
        public string Title { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string? PublishedDate { get; init; }
    }
}
