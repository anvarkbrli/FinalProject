namespace Ludora.Domain.Entities.User
{
    public sealed class ChatMessageUserState
    {
        public long MessageId { get; set; }
        public Guid UserId { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAtUtc { get; set; }
        public DateTime? PinnedAtUtc { get; set; }
        public DateTime? DeliveredAtUtc { get; set; }
        public DateTime? ReadAtUtc { get; set; }

        public ChatMessage Message { get; set; } = null!;
        public AppUser User { get; set; } = null!;
    }
}
