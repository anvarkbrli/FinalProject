namespace Ludora.Application.DTOs.FpsEstimator;

public sealed record FpsHardwareProfileDTO
{
    public bool IsConfigured { get; init; }
    public string? Gpu { get; init; }
    public string? Cpu { get; init; }
    public int? RamGb { get; init; }
    public int? VramGb { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
