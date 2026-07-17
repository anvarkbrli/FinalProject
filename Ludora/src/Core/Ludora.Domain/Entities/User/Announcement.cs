using Ludora.Domain.Entities.Base;

namespace Ludora.Domain.Entities.User;

public class Announcement : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }
    public AppUser CreatedByUser { get; set; } = null!;

    public bool IsPublished { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
}
