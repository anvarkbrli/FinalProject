using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.GameTracking
{
    public class GameCompany
    {
        public int GameId { get; set; }
        public Game Game { get; set; } = null!;
        public CatalogSource CompanySource { get; set; }
        public long CompanySourceId { get; set; }
        public GameCompanyRole Role { get; set; }
    }
}

