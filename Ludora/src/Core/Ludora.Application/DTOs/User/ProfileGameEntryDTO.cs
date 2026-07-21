using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record ProfileGameEntryDTO
{
    public CatalogSource Source { get; init; }
    public long GameId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Slug { get; init; }
    public string? CoverUrl { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public UserGameStatus? Status { get; init; }
    public bool IsFavorite { get; init; }
    public bool IsWishlisted { get; init; }
    public double? Rating { get; init; }
    public long? ReviewId { get; init; }
    public string? PlayedPlatformName { get; init; }
    public DateOnly? PlayedOn { get; init; }
    public int? PlaytimeMinutes { get; init; }
    public IReadOnlyList<string> Genres { get; init; } = [];
    public IReadOnlyList<string> Platforms { get; init; } = [];
    public IReadOnlyList<string> Companies { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
