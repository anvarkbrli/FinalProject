namespace Ludora.Domain.Entities.IGDB
{
    public class IgdbGenreOverride
    {
        public int Id { get; set; }
        public long IgdbGenreId { get; set; }
        public string? Name { get; set; }
        public bool IsHidden { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}


