using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{

    public record GameCreateDTO
    {
        public GameType GameType { get; init; } = GameType.MainGame;
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public DateOnly? ReleaseDate { get; init; }
        public string? CoverImageUrl { get; init; }
        public CatalogSource? ParentGameSource { get; init; }
        public long? ParentGameId { get; init; }
        public List<CompanyReferenceDTO> CompanyReferences { get; init; } = [];
        public List<CatalogReferenceDTO> GenreReferences { get; init; } = [];
        public List<CatalogReferenceDTO> PlatformReferences { get; init; } = [];
    }
}
