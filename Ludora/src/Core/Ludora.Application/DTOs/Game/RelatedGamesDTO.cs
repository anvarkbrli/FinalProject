using Ludora.Application.DTOs.GameList;

namespace Ludora.Application.DTOs.Game
{
    public record RelatedGamesDTO
    {
        public GameListItemDTO? ParentGame { get; init; }
        public List<GameListItemDTO> SeriesGames { get; init; } = [];
        public List<GameListItemDTO> FranchiseGames { get; init; } = [];
        public List<GameListItemDTO> Dlcs { get; init; } = [];
        public List<GameListItemDTO> Expansions { get; init; } = [];
        public List<GameListItemDTO> StandaloneExpansions { get; init; } = [];
        public List<GameListItemDTO> Bundles { get; init; } = [];
        public List<GameListItemDTO> Remakes { get; init; } = [];
        public List<GameListItemDTO> Remasters { get; init; } = [];
    }
}
