using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record ModeratorActionLogDTO
{
    public long Id { get; init; }
    public Guid AdminUserId { get; init; }
    public string AdminUserName { get; init; } = string.Empty;
    public Guid? TargetUserId { get; init; }
    public string TargetUserName { get; init; } = string.Empty;
    public long? UserReportId { get; init; }
    public int? UserWarningId { get; init; }
    public int? UserBanHistoryId { get; init; }
    public ModeratorActionType ActionType { get; init; }
    public UserReportStatus? PreviousReportStatus { get; init; }
    public UserReportStatus? NewReportStatus { get; init; }
    public string? Operation { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public string? ResourceName { get; init; }
    public string? Reason { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
