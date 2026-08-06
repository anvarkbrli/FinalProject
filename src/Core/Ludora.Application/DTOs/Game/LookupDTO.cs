using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record LookupDTO
    {
        public CatalogSource Source { get; init; }
        public long Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public bool IsOverridden { get; init; }
    }
}
