using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs;

public record GameListEntryDTO
{
    public CatalogSource Source { get; init; }
    public long Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? CoverImageUrl { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public double? AverageRating { get; init; }
    public int? Position { get; init; }
    public DateTime AddedAt { get; init; }
}
