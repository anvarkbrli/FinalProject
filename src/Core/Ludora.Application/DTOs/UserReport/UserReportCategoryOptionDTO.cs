using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record UserReportCategoryOptionDTO
{
    public UserReportCategory Value { get; init; }
    public string Label { get; init; } = string.Empty;
}
