using Ludora.Application.DTOs.GameList;

namespace Ludora.Application.DTOs.Home;

public sealed record HomeDiscoveryDTO
{
    public IReadOnlyList<GameListItemDTO> TrendingGames { get; init; } = [];
    public IReadOnlyList<GameListItemDTO> ComingSoonGames { get; init; } = [];
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
}
