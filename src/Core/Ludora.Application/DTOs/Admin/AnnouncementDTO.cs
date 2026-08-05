using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record AnnouncementDTO
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public Guid CreatedByUserId { get; init; }
    public string CreatedByUserName { get; init; } = string.Empty;
    public AnnouncementAudience Audience { get; init; }
    public bool IsPublished { get; init; }
    public DateTime? PublishedAtUtc { get; init; }
    public bool IsDeleted { get; init; }
    public int UniqueViewCount { get; init; }
    public int TargetUserCount { get; init; }
    public decimal ViewRatePercent { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
