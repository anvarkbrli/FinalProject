using Ludora.Domain.Enums;

namespace Ludora.Domain.Entities.User;

public sealed class ChatMessage
{
    public long Id { get; set; }
    public long ConversationId { get; set; }
    public Guid SenderUserId { get; set; }
    public ChatMessageKind Kind { get; set; }
    public string? Text { get; set; }
    public string? MediaStorageKey { get; set; }
    public string? MediaContentType { get; set; }
    public long? MediaLength { get; set; }
    public CatalogSource? GameSource { get; set; }
    public long? GameId { get; set; }
    public string? GameName { get; set; }
    public string? GameCoverUrl { get; set; }
    public DateTime SentAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public DateTime? DeletedForEveryoneAtUtc { get; set; }

    public ChatConversation Conversation { get; set; } = null!;
    public AppUser SenderUser { get; set; } = null!;
    public ICollection<ChatMessageUserState> UserStates { get; set; } = [];
}
