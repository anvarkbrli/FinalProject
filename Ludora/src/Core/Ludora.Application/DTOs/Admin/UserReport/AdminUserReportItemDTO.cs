using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record AdminUserReportItemDTO
{
    public long Id { get; init; }
    public Guid ReporterUserId { get; init; }
    public string ReporterUserName { get; init; } = string.Empty;
    public string? ReporterProfileImageUrl { get; init; }
    public UserReportCategory Category { get; init; }
    public string? Description { get; init; }
    public string? EvidenceImageUrl { get; init; }
    public IReadOnlyList<string> EvidenceImageUrls { get; init; } = [];
    public int EvidenceImageCount { get; init; }
    public UserReportContentType ReportedContentType { get; init; }
    public long? ReportedContentId { get; init; }
    public string? ReportedContentSnapshot { get; init; }
    public string? ReportedContentUrl { get; init; }
    public UserReportStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public Guid? ReviewedByUserId { get; init; }
    public string? ReviewedByUserName { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public string? AdminNote { get; init; }
}
