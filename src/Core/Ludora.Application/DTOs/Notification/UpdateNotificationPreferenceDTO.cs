using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.Notification;

public sealed class UpdateNotificationPreferenceDTO
{
    public NotificationAudience Audience { get; set; }
}
