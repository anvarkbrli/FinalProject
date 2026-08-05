using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.Notification;

public sealed record NotificationPreferenceDTO
{
    public NotificationType Type { get; init; }
    public NotificationAudience Audience { get; init; }
    public bool IsOverridden { get; init; }
}
