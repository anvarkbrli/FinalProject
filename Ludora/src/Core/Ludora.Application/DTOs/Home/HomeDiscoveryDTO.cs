using Ludora.Application.DTOs.GameList;

namespace Ludora.Application.DTOs.Home;

public sealed record HomeDiscoveryDTO
{
    public IReadOnlyList<GameListItemDTO> TrendingGames { get; init; } = [];
    public IReadOnlyList<HomeComingSoonGameDTO> ComingSoonGames { get; init; } = [];
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed record HomeComingSoonGameDTO
{
    public int WishlistRank { get; init; }
    public int SteamAppId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? CoverImageUrl { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public string StoreUrl { get; init; } = string.Empty;
}
