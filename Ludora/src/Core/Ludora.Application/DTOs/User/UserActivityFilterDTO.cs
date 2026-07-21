using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record UserActivityFilterDTO
{
    public UserActivityFeedCategory Category { get; init; } = UserActivityFeedCategory.All;
    public UserActivityFeedScope Scope { get; init; } = UserActivityFeedScope.All;
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
