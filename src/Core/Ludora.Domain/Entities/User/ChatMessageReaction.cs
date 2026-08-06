using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User
{
    public sealed class ChatMessageReaction
    {
        public long MessageId { get; set; }
        public Guid UserId { get; set; }
        public ChatReactionType ReactionType { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public ChatMessage Message { get; set; } = null!;
        public AppUser User { get; set; } = null!;
    }
}
