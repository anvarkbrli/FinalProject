using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record UserProfileGamesFilterDTO
{
    public string? Search { get; init; }
    public string? Genre { get; init; }
    public string? Company { get; init; }
    public string? Platform { get; init; }
    public string? PlayedPlatform { get; init; }
    public double? Rating { get; init; }
    public UserGameStatus? Status { get; init; }
    public int? ReleaseYear { get; init; }
    public UserProfileGameSortBy SortBy { get; init; } = UserProfileGameSortBy.RecentlyUpdated;
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 18;
}
