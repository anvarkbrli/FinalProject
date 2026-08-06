using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User
{
    public sealed class ModeratorActionLog
    {
        public long Id { get; set; }

        public Guid AdminUserId { get; set; }
        public AppUser AdminUser { get; set; } = null!;

        public Guid? TargetUserId { get; set; }
        public AppUser? TargetUser { get; set; }

        public long? UserReportId { get; set; }
        public UserReport? UserReport { get; set; }

        public int? UserWarningId { get; set; }
        public UserWarning? UserWarning { get; set; }

        public int? UserBanHistoryId { get; set; }
        public UserBanHistory? UserBanHistory { get; set; }

        public ModeratorActionType ActionType { get; set; }
        public UserReportStatus? PreviousReportStatus { get; set; }
        public UserReportStatus? NewReportStatus { get; set; }
        public string? Operation { get; set; }
        public string? ResourceType { get; set; }
        public string? ResourceId { get; set; }
        public string? ResourceName { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
