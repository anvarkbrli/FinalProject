using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record GameFilterDTO
    {
        public CatalogSource? Source { get; init; }
        public string? SearchName { get; init; }
        public CatalogSource? CompanySource { get; init; }
        public long? CompanyId { get; init; }
        public CatalogSource? GenreSource { get; init; }
        public long? GenreId { get; init; }
        public CatalogSource? PlatformSource { get; init; }
        public long? PlatformId { get; init; }
        public int? ReleaseYear { get; init; }
        public string? SortBy { get; init; }
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 20;
        public bool IncludeRelatedContent { get; init; }
    }
}


