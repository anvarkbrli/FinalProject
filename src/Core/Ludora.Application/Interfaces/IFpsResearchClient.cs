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
}
