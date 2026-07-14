

namespace Ludora.Domain.Entities
{
    public class GameListItem
    {
        public int GameListId { get; set; }

        public GameList GameList { get; set; } = null!;

        public long IgdbGameId { get; set; }

        public string GameTitle { get; set; } = null!;

        public string? CoverImageUrl { get; set; }

        public DateOnly? ReleaseDate { get; set; }

        public double? IgdbRating { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
