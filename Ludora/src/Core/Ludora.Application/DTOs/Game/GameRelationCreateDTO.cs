using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record GameRelationCreateDTO
    {
        public CatalogSource RelatedSource { get; init; }
        public long RelatedId { get; init; }
        public GameType RelationType { get; init; }
    }
}
