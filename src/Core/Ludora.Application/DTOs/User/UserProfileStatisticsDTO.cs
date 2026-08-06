namespace Ludora.Application.DTOs
{
    public record UserProfileStatisticsDTO
    {
        public long TotalPlaytimeMinutes { get; init; }
        public int ReviewCount { get; init; }
        public int RatingCount { get; init; }
        public double? AverageRating { get; init; }
        public IReadOnlyList<UserRatingDistributionDTO> RatingDistribution { get; init; } = [];
    }
}
