using Ludora.Application.DTOs.GameList;
using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.Home
{
    public sealed record HomeDiscoveryDTO
    {
        public IReadOnlyList<GameListItemDTO> TrendingGames { get; init; } = [];
        public IReadOnlyList<HomeComingSoonGameDTO> ComingSoonGames { get; init; } = [];
        public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
    }

    public sealed record HomeComingSoonGameDTO
    {
        public int AnticipationRank { get; init; }
        public CatalogSource Source { get; init; }
        public long Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? CoverImageUrl { get; init; }
        public DateOnly? ReleaseDate { get; init; }
    }
}
