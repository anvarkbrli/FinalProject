using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record CatalogReferenceDTO
    {
        public CatalogSource Source { get; init; }

        public long Id { get; init; }
    }
}
