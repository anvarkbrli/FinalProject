using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.Contact;

public sealed record ContactMessageDTO
{
    public int Id { get; init; }
    public Guid? UserId { get; init; }
    public string? UserName { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Language { get; init; } = "az";
    public ContactMessageStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public Guid? RepliedByAdminUserId { get; init; }
    public string? RepliedByAdminUserName { get; init; }
    public string? ReplySubject { get; init; }
    public string? ReplyBody { get; init; }
    public DateTime? ReplyQueuedAtUtc { get; init; }
    public DateTime? RepliedAtUtc { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
}
