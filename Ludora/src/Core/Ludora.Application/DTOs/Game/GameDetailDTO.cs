using Ludora.Application.DTOs.Game;
using Ludora.Domain.Enums;


namespace Ludora.Application.DTOs
{
    public record GameDetailDTO
    {
        public CatalogSource Source { get; init; }
        public long Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public DateOnly? ReleaseDate { get; init; }
        public string? CoverImageUrl { get; init; }
        public int PlayCount { get; init; }
        public double? AverageRating { get; init; }
        public bool IsOverridden { get; init; }
        public List<CompanyLookupDTO> Companies { get; init; } = [];
        public List<LookupDTO> Genres { get; init; } = [];
        public List<LookupDTO> Platforms { get; init; } = [];
        public GameType GameType { get; init; } = GameType.MainGame;
        public GameCommunityStatsDTO CommunityStats { get; init; } = new();
        public RelatedGamesDTO RelatedGames { get; init; } = new();
    }
}
