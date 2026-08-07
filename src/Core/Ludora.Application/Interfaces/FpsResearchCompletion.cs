namespace Ludora.Application.Interfaces
{
    public sealed record FpsResearchCompletion
    {
        public string Content { get; init; } = string.Empty;
        public IReadOnlyCollection<FpsResearchSource> Sources { get; init; } = [];
    }
}
