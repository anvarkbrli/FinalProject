using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User;

public sealed class BadgeDefinition
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "Award";
    public string? IconUrl { get; set; }
    public string Color { get; set; } = "#8b5cf6";
    public BadgeMetric Metric { get; set; }
    public int Threshold { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<UserBadge> UserBadges { get; set; } = [];
}
