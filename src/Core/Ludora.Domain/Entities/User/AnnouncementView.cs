namespace Ludora.Domain.Entities.User;

public sealed class AnnouncementView
{
    public long Id { get; set; }

    public int AnnouncementId { get; set; }
    public Announcement Announcement { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public DateTime FirstViewedAtUtc { get; set; }
}
