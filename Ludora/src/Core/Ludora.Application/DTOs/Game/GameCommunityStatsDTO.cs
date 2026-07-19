namespace Ludora.Application.DTOs.Game;

public sealed record GameCommunityStatsDTO
{
    public double? AverageRating { get; init; }
    public int RatingCount { get; init; }
    public IReadOnlyList<GameRatingDistributionItemDTO> RatingDistribution { get; init; } = [];
    public int PlayingCount { get; init; }
    public int PlanToPlayCount { get; init; }
    public int PlayedCount { get; init; }
    public int DroppedCount { get; init; }
    public int WishlistCount { get; init; }
    public int FavoriteCount { get; init; }
}
