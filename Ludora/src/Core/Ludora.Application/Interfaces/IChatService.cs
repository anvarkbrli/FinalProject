using Ludora.Application.DTOs.Chat;
using Ludora.Domain.Enums;

namespace Ludora.Application.Interfaces;

public interface IChatService
{
    Task<IReadOnlyList<ChatConversationDTO>> GetConversationsAsync(
        Guid userId,
        bool archived = false,
        string? search = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatConversationDTO>> GetMessageRequestsAsync(
        Guid userId,
        string? search = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetConversationPartnerIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<ChatMessagePageDTO> GetMessagesAsync(
        Guid userId,
        long conversationId,
        long? beforeMessageId,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<ChatMessageDTO> GetMessageAsync(
        Guid userId,
        long conversationId,
        long messageId,
        CancellationToken cancellationToken = default);
    Task<ChatMessageSearchPageDTO> SearchMessagesAsync(
        Guid userId,
        long conversationId,
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<ChatSendMessageResultDTO> SendMessageAsync(
        Guid senderUserId,
        ChatSendMessageDTO dto,
        CancellationToken cancellationToken = default);
    Task<ChatForwardMessagesResultDTO> ForwardMessageAsync(
        Guid userId,
        long sourceMessageId,
        IReadOnlyCollection<long> targetConversationIds,
        CancellationToken cancellationToken = default);
    Task<ChatRequestDecisionDTO> AcceptRequestAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default);
    Task<ChatRequestDecisionDTO> RejectRequestAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default);
    Task<ChatReadReceiptDTO> MarkReadAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatReadReceiptDTO>> MarkDeliveredAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessageDTO>> GetPinnedMessagesAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default);
    Task<ChatMessageDeletedDTO> DeleteMessageAsync(
        Guid userId,
        long messageId,
        ChatMessageDeleteScope scope,
        CancellationToken cancellationToken = default);
    Task<ChatMessagePinChangedDTO> SetMessagePinAsync(
        Guid userId,
        long messageId,
        bool isPinned,
        CancellationToken cancellationToken = default);
    Task<ChatMessageReactionChangedDTO> SetMessageReactionAsync(
        Guid userId,
        long messageId,
        ChatReactionType? reactionType,
        CancellationToken cancellationToken = default);
    Task<ChatConversationArchiveChangedDTO> SetConversationArchivedAsync(
        Guid userId,
        long conversationId,
        bool isArchived,
        CancellationToken cancellationToken = default);
    Task<ChatConversationParticipantsDTO> GetParticipantsAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default);
    Task<StoredChatMediaDTO> OpenMediaAsync(
        Guid userId,
        long messageId,
        long? mediaId = null,
        CancellationToken cancellationToken = default);
}
