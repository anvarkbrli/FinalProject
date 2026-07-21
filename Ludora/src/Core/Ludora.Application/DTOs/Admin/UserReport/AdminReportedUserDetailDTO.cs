namespace Ludora.Application.DTOs;

public record AdminReportedUserDetailDTO
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? ProfileImageUrl { get; init; }
    public int TotalReportCount { get; init; }
    public int PendingReportCount { get; init; }
    public int WarningCount { get; init; }
    public bool IsBanned { get; init; }
    public bool IsPermanentlyBanned { get; init; }
    public DateTime? BannedUntilUtc { get; init; }
    public string? BanReason { get; init; }
    public IReadOnlyList<UserReportCategoryCountDTO> Categories { get; init; } = [];
    public IReadOnlyList<AdminUserReportItemDTO> Reports { get; init; } = [];
}
