
namespace Ludora.Application.DTOs
{
    public record GameListEntryDTO
    {
        public int Id { get; set; }

        public string Title { get; set; } = null!;

        public string CoverImageUrl { get; set; } = null!;

        public DateOnly ReleaseDate { get; set; }

        public double AverageRating { get; set; }

        public DateTime AddedAt { get; set; }
    }
}
