using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record AdminUserReportStatusUpdateDTO
    {
        public UserReportStatus Status { get; init; }
        public string? AdminNote { get; init; }
    }
}
