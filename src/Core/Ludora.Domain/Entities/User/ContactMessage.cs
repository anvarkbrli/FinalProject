using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User
{
    public sealed class ContactMessage
    {
        public int Id { get; set; }
        public Guid? UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Language { get; set; } = "az";
        public ContactMessageStatus Status { get; set; } = ContactMessageStatus.New;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }
        public Guid? RepliedByAdminUserId { get; set; }
        public string? ReplySubject { get; set; }
        public string? ReplyBody { get; set; }
        public DateTime? ReplyQueuedAtUtc { get; set; }
        public DateTime? RepliedAtUtc { get; set; }
        public DateTime? ClosedAtUtc { get; set; }
    }
}
