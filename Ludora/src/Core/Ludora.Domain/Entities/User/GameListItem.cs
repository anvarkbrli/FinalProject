using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User
{
    public class GameListItem
    {
        public int GameListId { get; set; }
        public GameList GameList { get; set; } = null!;
        public CatalogSource GameSource { get; set; }
        public long GameSourceId { get; set; }
        public string GameTitle { get; set; } = string.Empty;
        public string? CoverImageUrl { get; set; }
        public DateOnly? ReleaseDate { get; set; }
        public double? Rating { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}


