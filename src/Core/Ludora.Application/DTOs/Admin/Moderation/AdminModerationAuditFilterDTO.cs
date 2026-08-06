using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record AdminModerationAuditFilterDTO
    {
        public Guid? AdminUserId { get; init; }
        public Guid? TargetUserId { get; init; }
        public long? UserReportId { get; init; }
        public ModeratorActionType? ActionType { get; init; }
        public DateTime? FromUtc { get; init; }
        public DateTime? ToUtc { get; init; }
        public string? Search { get; init; }
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 20;
    }
}
