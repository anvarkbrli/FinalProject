namespace Ludora.Domain.Entities.User;

public sealed class NotificationOutboxMessage
{
    public long Id { get; set; }

    public long NotificationId { get; set; }
    public Notification Notification { get; set; } = null!;

    public string EventName { get; set; } = "NotificationCreated";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? LastError { get; set; }

    public Guid? LockToken { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
}
