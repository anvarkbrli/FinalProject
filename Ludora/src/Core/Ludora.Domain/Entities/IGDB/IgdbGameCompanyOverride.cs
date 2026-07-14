using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.IGDB
{
    public class IgdbGameCompanyOverride
    {
        public int IgdbGameOverrideId { get; set; }
        public IgdbGameOverride GameOverride { get; set; } = null!;
        public CatalogSource CompanySource { get; set; }
        public long CompanySourceId { get; set; }
        public GameCompanyRole Role { get; set; }
    }
}

