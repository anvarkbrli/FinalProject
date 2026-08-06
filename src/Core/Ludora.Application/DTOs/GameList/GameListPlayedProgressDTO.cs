namespace Ludora.Application.DTOs
{
    public sealed record GameListPlayedProgressDTO
    {
        public int PlayedGameCount { get; init; }

        public int TotalGameCount { get; init; }
    }
}
