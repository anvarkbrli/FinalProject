using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record GameFilterDTO
    {
        public CatalogSource? Source { get; init; }
        public string? SearchName { get; init; }

        // Legacy single-value filters are kept so old links and clients continue to work.
        public CatalogSource? CompanySource { get; init; }
        public long? CompanyId { get; init; }
        public CatalogSource? GenreSource { get; init; }
        public long? GenreId { get; init; }
        public CatalogSource? PlatformSource { get; init; }
        public long? PlatformId { get; init; }

        // Comma-separated catalog references, for example: "Igdb:12,Igdb:31".
        // Values inside the same category use OR logic. Different categories use AND logic.
        public string? CompanyFilters { get; init; }
        public string? GenreFilters { get; init; }
        public string? PlatformFilters { get; init; }

        public int? ReleaseYear { get; init; }
        public bool ReleasedOnly { get; init; }
        public string? SortBy { get; init; }
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 16;
        public bool IncludeRelatedContent { get; init; }

        public IReadOnlyList<CatalogReferenceDTO> GetCompanyReferences() =>
            ParseReferences(CompanyFilters, CompanySource, CompanyId);

        public IReadOnlyList<CatalogReferenceDTO> GetGenreReferences() =>
            ParseReferences(GenreFilters, GenreSource, GenreId);

        public IReadOnlyList<CatalogReferenceDTO> GetPlatformReferences() =>
            ParseReferences(PlatformFilters, PlatformSource, PlatformId);

        private static IReadOnlyList<CatalogReferenceDTO> ParseReferences(
            string? serializedReferences,
            CatalogSource? legacySource,
            long? legacyId)
        {
            var references = new List<CatalogReferenceDTO>();

            if (!string.IsNullOrWhiteSpace(serializedReferences))
            {
                foreach (var token in serializedReferences.Split(
                             ',',
                             StringSplitOptions.TrimEntries |
                             StringSplitOptions.RemoveEmptyEntries))
                {
                    var separatorIndex = token.IndexOf(':');
                    if (separatorIndex <= 0 || separatorIndex >= token.Length - 1)
                    {
                        continue;
                    }

                    var sourceValue = token[..separatorIndex];
                    var idValue = token[(separatorIndex + 1)..];

                    if (!Enum.TryParse<CatalogSource>(sourceValue, true, out var source) ||
                        !long.TryParse(idValue, out var id))
                    {
                        continue;
                    }

                    references.Add(new CatalogReferenceDTO
                    {
                        Source = source,
                        Id = id
                    });
                }
            }

            if (legacySource.HasValue && legacyId.HasValue)
            {
                references.Add(new CatalogReferenceDTO
                {
                    Source = legacySource.Value,
                    Id = legacyId.Value
                });
            }

            return references
                .DistinctBy(reference => new { reference.Source, reference.Id })
                .ToArray();
        }
    }
}
