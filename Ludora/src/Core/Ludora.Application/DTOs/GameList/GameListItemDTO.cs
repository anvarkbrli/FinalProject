
namespace Ludora.Application.DTOs.GameList
{
    public record GameListItemDTO
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string CoverImageUrl { get; set; }
        public string DeveloperCompanyName { get; set; }
        public int PlayCount { get; set; }
        public double AverageRating { get; set; }
        public DateOnly ReleaseDate { get; set; }
        public List<string> Genres { get; set; }
        public List<string> Platforms { get; set; }
    }
}
