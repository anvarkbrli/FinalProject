using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record ReviewCreateDTO
{
    public string Content { get; init; } = string.Empty;
    public double? Rating { get; init; }
    public UserGameStatus? Status { get; init; }
    public bool ContainsSpoilers { get; init; }
    public DateOnly? PlayedOn { get; init; }
    public CatalogSource? PlatformSource { get; init; }
    public long? PlatformId { get; init; }
    public int? PlaytimeMinutes { get; init; }
    public bool ShowHardwareProfile { get; init; }
    public string? HardwareGpu { get; init; }
    public string? HardwareCpu { get; init; }
    public int? HardwareRamGb { get; init; }
    public int? HardwareVramGb { get; init; }
}
