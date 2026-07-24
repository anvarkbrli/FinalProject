using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.Badge;

public sealed record BadgeDTO
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public BadgeMetric Metric { get; init; }
    public int Threshold { get; init; }
    public bool IsActive { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed record UserBadgeDTO
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public int MetricValueAtAward { get; init; }
    public DateTime AwardedAtUtc { get; init; }
}

public sealed record BadgeUpsertDTO
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = "Award";
    public string Color { get; init; } = "#8b5cf6";
    public BadgeMetric Metric { get; init; }
    public int Threshold { get; init; }
    public bool IsActive { get; init; } = true;
    public int DisplayOrder { get; init; }
}
