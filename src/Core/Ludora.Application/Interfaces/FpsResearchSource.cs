namespace Ludora.Application.Interfaces
{
    public sealed record FpsResearchSource
    {
        public string Title { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string? PublishedDate { get; init; }
    }
}
