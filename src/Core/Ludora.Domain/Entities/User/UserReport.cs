using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User
{
    public class UserReport
    {
        public long Id { get; set; }

        public Guid ReporterUserId { get; set; }
        public AppUser ReporterUser { get; set; } = null!;

        public Guid ReportedUserId { get; set; }
        public AppUser ReportedUser { get; set; } = null!;

        public UserReportCategory Category { get; set; }
        public string? Description { get; set; }
        public string? EvidenceImageStorageKey { get; set; }

        public UserReportContentType ReportedContentType { get; set; }
        public long? ReportedContentId { get; set; }
        public string? ReportedContentSnapshot { get; set; }
        public string? ReportedContentUrl { get; set; }

        public UserReportStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public Guid? ReviewedByUserId { get; set; }
        public AppUser? ReviewedByUser { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
        public string? AdminNote { get; set; }

        public ICollection<ModeratorActionLog> ModeratorActions { get; set; } = [];
        public ICollection<UserReportEvidence> EvidenceImages { get; set; } = [];
    }
}
