namespace Ludora.Domain.Entities.User;

public sealed class ChatConversationUserState
{
    public long ConversationId { get; set; }
    public Guid UserId { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public ChatConversation Conversation { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
