namespace Ludora.Application.DTOs.FpsEstimator;

public sealed record FpsEstimateResponseDTO
{
    public string NormalizedGameName { get; init; } = string.Empty;
    public string NormalizedGpuName { get; init; } = string.Empty;
    public string NormalizedCpuName { get; init; } = string.Empty;
    public string Resolution { get; init; } = string.Empty;
    public string GraphicsPreset { get; init; } = string.Empty;
    public int AverageFps { get; init; }
    public int OnePercentLowFps { get; init; }
    public int MinimumExpectedFps { get; init; }
    public int MaximumExpectedFps { get; init; }
    public int ConfidencePercent { get; init; }
    public string PerformanceLevel { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Recommendations { get; init; } = [];
    public IReadOnlyCollection<string> ImportantFactors { get; init; } = [];
    public string EvidenceQuality { get; init; } = string.Empty;
    public string EvidenceSummary { get; init; } = string.Empty;
    public int BenchmarkSampleCount { get; init; }
    public IReadOnlyCollection<FpsBenchmarkSourceDTO> BenchmarkSources { get; init; } = [];
    public string Disclaimer { get; init; } = string.Empty;
    public bool IsCached { get; init; }
}
