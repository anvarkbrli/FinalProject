namespace Ludora.Application.DTOs.FpsEstimator;

public sealed record FpsBenchmarkSourceDTO
{
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? PublishedDate { get; init; }
}
