using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record AdminUserReportFilterDTO
{
    public string? Search { get; init; }
    public UserReportCategory? Category { get; init; }
    public UserReportStatus? Status { get; init; }
    public bool? IsBanned { get; init; }
    public string? SortBy { get; init; } = "mostReported";
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
