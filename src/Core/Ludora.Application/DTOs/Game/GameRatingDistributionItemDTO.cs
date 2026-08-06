namespace Ludora.Application.DTOs.Game
{
    public sealed record GameRatingDistributionItemDTO
    {
        public double Rating { get; init; }
        public int Count { get; init; }
    }
}
