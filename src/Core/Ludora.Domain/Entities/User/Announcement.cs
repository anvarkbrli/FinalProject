using Ludora.Domain.Entities.Base;
using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User
{
    public class Announcement : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;

        public Guid CreatedByUserId { get; set; }
        public AppUser CreatedByUser { get; set; } = null!;

        public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.Everyone;
        public bool IsPublished { get; set; }
        public DateTime? PublishedAtUtc { get; set; }

        public ICollection<AnnouncementView> Views { get; set; } = [];
    }
}
