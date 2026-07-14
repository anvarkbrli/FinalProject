

namespace Ludora.Domain.Entities.IGDB
{
    public class IgdbCompanyOverride
    {
        public int Id { get; set; }
        public long IgdbCompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsHidden { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
