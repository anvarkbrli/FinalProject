using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.Notification
{
    public sealed record NotificationDTO
    {
        public long Id { get; init; }
        public NotificationType Type { get; init; }
        public NotificationPriority Priority { get; init; }
        public NotificationEntityType EntityType { get; init; }
        public string? EntityId { get; init; }

        public Guid? ActorUserId { get; init; }
        public string? ActorUserName { get; init; }
        public string? ActorProfileImageUrl { get; init; }

        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string? ActionUrl { get; init; }
        public string? DataJson { get; init; }

        public bool IsAdminNotification { get; init; }
        public bool IsRead { get; init; }
        public DateTime? ReadAtUtc { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }
}
