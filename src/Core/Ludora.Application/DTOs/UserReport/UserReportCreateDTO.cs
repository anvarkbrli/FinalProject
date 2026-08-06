using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record UserReportCreateDTO
    {
        public UserReportCategory Category { get; init; }
        public string? Description { get; init; }
        public IReadOnlyList<UserReportEvidenceUploadDTO> EvidenceImages { get; init; } = [];
        public UserReportContentType ReportedContentType { get; init; } = UserReportContentType.UserProfile;
        public long? ReportedContentId { get; init; }
    }
}
