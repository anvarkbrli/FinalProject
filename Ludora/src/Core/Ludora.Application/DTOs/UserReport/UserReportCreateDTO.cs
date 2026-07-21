using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record UserReportCreateDTO
{
    public UserReportCategory Category { get; init; }
    public string? Description { get; init; }
    public UserReportEvidenceUploadDTO? EvidenceImage { get; init; }
}
