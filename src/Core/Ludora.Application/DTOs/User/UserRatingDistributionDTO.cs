namespace Ludora.Application.DTOs
{
    public record UserRatingDistributionDTO
    {
        public double Rating { get; init; }
        public int Count { get; init; }
    }
}
