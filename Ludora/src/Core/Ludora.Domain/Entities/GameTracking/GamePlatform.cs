using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.GameTracking
{
    public class GamePlatform
    {
        public int GameId { get; set; }
        public Game Game { get; set; } = null!;
        public CatalogSource PlatformSource { get; set; }
        public long PlatformSourceId { get; set; }
    }
}

