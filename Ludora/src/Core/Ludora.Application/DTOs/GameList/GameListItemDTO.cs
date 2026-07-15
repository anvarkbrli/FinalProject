
using Ludora.Application.DTOs;
using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.GameList
{
    public record GameListItemDTO
    {
        public CatalogSource Source { get; init; }
        public long Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? CoverImageUrl { get; init; }
        public DateOnly? ReleaseDate { get; init; }
        public double? AverageRating { get; init; }
        public bool IsOverridden { get; init; }
        public List<LookupDTO> Genres { get; init; } = [];
        public List<LookupDTO> Platforms { get; init; } = [];
    }
}
