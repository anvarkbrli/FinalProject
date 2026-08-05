using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record UserPlaytimeChartGameDTO
{
    public CatalogSource Source { get; init; }
    public long GameId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? CoverUrl { get; init; }
    public long PlaytimeMinutes { get; init; }
}

public record UserPlaytimeChartPointDTO
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public long PlaytimeMinutes { get; init; }
    public int GameCount { get; init; }
    public IReadOnlyList<UserPlaytimeChartGameDTO> Games { get; init; } = [];
}

public record UserTopPlayedGameDTO
{
    public CatalogSource Source { get; init; }
    public long GameId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? CoverUrl { get; init; }
    public long PlaytimeMinutes { get; init; }
}

public record UserTopPlayedCategoryDTO
{
    public string Name { get; init; } = string.Empty;
    public long PlaytimeMinutes { get; init; }
    public double Percentage { get; init; }
}

public record UserPlaytimeStatisticsDTO
{
    public UserPlaytimePeriod Period { get; init; }
    public long PeriodTotalPlaytimeMinutes { get; init; }
    public long AllTimeTotalPlaytimeMinutes { get; init; }
    public IReadOnlyList<UserPlaytimeChartPointDTO> Chart { get; init; } = [];
    public UserTopPlayedGameDTO? TopGame { get; init; }
    public UserTopPlayedCategoryDTO? TopGenre { get; init; }
    public UserTopPlayedCategoryDTO? TopPlatform { get; init; }
}
