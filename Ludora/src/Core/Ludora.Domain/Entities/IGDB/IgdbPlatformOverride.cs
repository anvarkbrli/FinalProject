namespace Ludora.Domain.Entities.IGDB
{
    public class IgdbPlatformOverride
    {
        public int Id { get; set; }
        public long IgdbPlatformId { get; set; }
        public string? Name { get; set; }
        public bool IsHidden { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}

