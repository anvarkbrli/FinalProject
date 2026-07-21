namespace Ludora.Application.DTOs;

public record AdminUserDetailDTO
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Bio { get; init; }
    public string? ProfileImageUrl { get; init; }
    public string Role { get; init; } = string.Empty;
    public bool IsPrivate { get; init; }
    public bool IsBanned { get; init; }
    public bool IsPermanentlyBanned { get; init; }
    public DateTime? BannedUntilUtc { get; init; }
    public string? BanReason { get; init; }
    public DateTime? BannedAtUtc { get; init; }
    public Guid? BannedByUserId { get; init; }
    public int FollowerCount { get; init; }
    public int FollowingCount { get; init; }
    public int PostCount { get; init; }
    public int DeletedPostCount { get; init; }
    public int DiscussionCount { get; init; }
    public int ReplyCount { get; init; }
    public int WarningCount { get; init; }
    public int UnreadWarningCount { get; init; }
    public int ReportCount { get; init; }
    public int PendingReportCount { get; init; }
    public DateTime CreatedAt { get; init; }
}
