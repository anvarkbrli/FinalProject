using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record UserReportDTO
{
    public long Id { get; init; }
    public Guid ReportedUserId { get; init; }
    public string ReportedUserName { get; init; } = string.Empty;
    public UserReportCategory Category { get; init; }
    public string? Description { get; init; }
    public bool HasEvidenceImage { get; init; }
    public int EvidenceImageCount { get; init; }
    public UserReportContentType ReportedContentType { get; init; }
    public long? ReportedContentId { get; init; }
    public string? ReportedContentSnapshot { get; init; }
    public string? ReportedContentUrl { get; init; }
    public UserReportStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
