using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User
{
    public sealed class Notification
    {
        public long Id { get; set; }

        public Guid RecipientUserId { get; set; }
        public AppUser RecipientUser { get; set; } = null!;

        public Guid? ActorUserId { get; set; }
        public AppUser? ActorUser { get; set; }

        public NotificationType Type { get; set; }
        public NotificationPriority Priority { get; set; }
        public NotificationEntityType EntityType { get; set; }
        public string? EntityId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ActionUrl { get; set; }
        public string? DataJson { get; set; }

        public bool IsRead { get; set; }
        public DateTime? ReadAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public NotificationOutboxMessage? OutboxMessage { get; set; }
    }
}
