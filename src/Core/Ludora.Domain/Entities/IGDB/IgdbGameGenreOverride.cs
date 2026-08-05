using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.IGDB
{
    public class IgdbGameGenreOverride
    {
        public int IgdbGameOverrideId { get; set; }
        public IgdbGameOverride GameOverride { get; set; } = null!;
        public CatalogSource GenreSource { get; set; }
        public long GenreSourceId { get; set; }
    }
}

