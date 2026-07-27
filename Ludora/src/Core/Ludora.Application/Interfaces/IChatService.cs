using Ludora.Domain.Enums;
using Ludora.Application.DTOs.Chat;

namespace Ludora.Application.Interfaces;

public interface IChatService
{
    Task<IReadOnlyList<ChatConversationDTO>> GetConversationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatConversationDTO>> GetMessageRequestsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<ChatMessagePageDTO> GetMessagesAsync(
        Guid userId,
        long conversationId,
        long? beforeMessageId,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<ChatMessageDTO> SendMessageAsync(
        Guid senderUserId,
        ChatSendMessageDTO dto,
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
    Task<StoredChatMediaDTO> OpenMediaAsync(
        Guid userId,
        long messageId,
        CancellationToken cancellationToken = default);
}
