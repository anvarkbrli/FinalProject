namespace Ludora.Domain.Entities.User
{
    public sealed class ChatConversationMember
    {
        public long ConversationId { get; set; }
        public Guid UserId { get; set; }
        public Guid AddedByUserId { get; set; }
        public DateTime JoinedAtUtc { get; set; }

        public ChatConversation Conversation { get; set; } = null!;
        public AppUser User { get; set; } = null!;
        public AppUser AddedByUser { get; set; } = null!;
    }
}
