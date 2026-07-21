using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.Notification;

public sealed class NotificationFilterDTO
{
    public bool? IsRead { get; set; }
    public NotificationType? Type { get; set; }
    public NotificationPriority? Priority { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
