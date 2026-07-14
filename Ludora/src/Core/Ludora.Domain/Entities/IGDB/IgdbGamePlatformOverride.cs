using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.IGDB
{
    public class IgdbGamePlatformOverride
    {
        public int IgdbGameOverrideId { get; set; }
        public IgdbGameOverride GameOverride { get; set; } = null!;
        public CatalogSource PlatformSource { get; set; }
        public long PlatformSourceId { get; set; }
    }
}

