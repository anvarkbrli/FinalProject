using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public sealed record UserGameDashboardDTO
    {
        public IReadOnlyDictionary<UserGameStatus, int> StatusCounts { get; init; } =
            new Dictionary<UserGameStatus, int>();
        public int WishlistCount { get; init; }
        public double? AverageRating { get; init; }
        public int RatingCount { get; init; }
        public IReadOnlyList<UserRatingDistributionDTO> RatingDistribution { get; init; } = [];
        public UserGameDTO? RecentGame { get; init; }
        public int ReviewCount { get; init; }
        public int GameListCount { get; init; }
    }
}
