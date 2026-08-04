using Ludora.Application.DTOs.GameList;

namespace Ludora.Application.DTOs.Home;

public sealed record HomeInterestRecommendationDTO
{
    public GameListItemDTO Game { get; init; } = new();
    public string Reason { get; init; } = string.Empty;
    public int MatchScore { get; init; }
}

public sealed record HomeInterestRecommendationsDTO
{
    public string Language { get; init; } = string.Empty;
    public IReadOnlyList<HomeInterestRecommendationDTO> Items { get; init; } = [];
    public string Summary { get; init; } = string.Empty;
    public bool IsAiGenerated { get; init; }
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
}
