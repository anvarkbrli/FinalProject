using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User;

public sealed class BackgroundJob
{
    public long Id { get; set; }
    public BackgroundJobType Type { get; set; }
    public Guid UserId { get; set; }
    public BackgroundJobStatus Status { get; set; } = BackgroundJobStatus.Pending;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime AvailableAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public Guid? LockToken { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public string? LastError { get; set; }
}
