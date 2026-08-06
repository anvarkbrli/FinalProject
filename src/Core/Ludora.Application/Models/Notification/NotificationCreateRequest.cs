using Ludora.Domain.Enums;

namespace Ludora.Application.Models.Notification
{
    public sealed record NotificationCreateRequest
    {
        public required Guid RecipientUserId { get; init; }
        public Guid? ActorUserId { get; init; }
        public required NotificationType Type { get; init; }
        public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;
        public NotificationEntityType EntityType { get; init; } = NotificationEntityType.None;
        public string? EntityId { get; init; }
        public required string Title { get; init; }
        public required string Message { get; init; }
        public string? ActionUrl { get; init; }
        public string? DataJson { get; init; }
        public bool BypassPreferences { get; init; }
    }
}
