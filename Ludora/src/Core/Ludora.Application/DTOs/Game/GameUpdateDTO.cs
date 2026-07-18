using Ludora.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.DTOs
{
    public record GameUpdateDTO
    {
        public GameType? GameType { get; init; }
        public string? Title { get; init; }
        public string? Description { get; init; }
        public DateOnly? ReleaseDate { get; init; }
        public string? CoverImageUrl { get; init; }
        public List<CompanyReferenceDTO>? CompanyReferences { get; init; }
        public List<CatalogReferenceDTO>? GenreReferences { get; init; }
        public List<CatalogReferenceDTO>? PlatformReferences { get; init; }
    }
}
