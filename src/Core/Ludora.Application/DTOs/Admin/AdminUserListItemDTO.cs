namespace Ludora.Application.DTOs
{
    public record AdminUserListItemDTO
    {
        public Guid Id { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? ProfileImageUrl { get; init; }
        public string Role { get; init; } = string.Empty;
        public bool IsPrivate { get; init; }
        public bool IsOnline { get; init; }
        public bool IsBanned { get; init; }
        public bool IsPermanentlyBanned { get; init; }
        public DateTime? BannedUntilUtc { get; init; }
        public int WarningCount { get; init; }
        public int ReportCount { get; init; }
        public int PendingReportCount { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
