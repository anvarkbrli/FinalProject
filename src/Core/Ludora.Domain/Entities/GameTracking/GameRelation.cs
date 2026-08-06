using Ludora.Domain.Entities.Base;
using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.GameTracking
{
    public class GameRelation : BaseEntity
    {
        public CatalogSource BaseGameSource { get; set; }
        public long BaseGameId { get; set; }
        public CatalogSource RelatedGameSource { get; set; }
        public long RelatedGameId { get; set; }
        public GameType RelationType { get; set; }
    }
}
