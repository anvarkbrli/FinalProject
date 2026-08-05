using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record UserReportCategoryCountDTO
{
    public UserReportCategory Category { get; init; }
    public int Count { get; init; }
}
