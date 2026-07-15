using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs
{
    public record UserGameDTO
    {
        public CatalogSource Source { get; set; }
        public long GameId { get; set; }
        public string Name { get; set; } = null!;
        public string? Slug { get; set; }
        public string? CoverUrl { get; set; }
        public DateOnly? ReleaseDate { get; set; }
        public UserGameStatus? Status { get; set; }
        public bool IsFavorite { get; set; }
        public bool IsWishlisted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
