using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User;

public sealed class NotificationPreference
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public NotificationType Type { get; set; }
    public NotificationAudience Audience { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
