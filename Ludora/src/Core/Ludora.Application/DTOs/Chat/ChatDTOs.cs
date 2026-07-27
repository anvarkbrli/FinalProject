using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.Chat;

public sealed record ChatUserDTO
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? ProfileImageUrl { get; init; }
}

public sealed record ChatMessageDTO
{
    public long Id { get; init; }
    public long ConversationId { get; init; }
    public Guid SenderUserId { get; init; }
    public ChatMessageKind Kind { get; init; }
    public string? Text { get; init; }
    public string? MediaUrl { get; init; }
    public string? MediaContentType { get; init; }
    public CatalogSource? GameSource { get; init; }
    public long? GameId { get; init; }
    public string? GameName { get; init; }
    public string? GameCoverUrl { get; init; }
    public string? GameDetailUrl { get; init; }
    public DateTime SentAtUtc { get; init; }
    public DateTime? DeliveredAtUtc { get; init; }
    public DateTime? ReadAtUtc { get; init; }
    public ChatMessageDeliveryStatus DeliveryStatus { get; init; }
    public bool IsDeletedForEveryone { get; init; }
    public DateTime? DeletedForEveryoneAtUtc { get; init; }
    public bool IsPinnedByCurrentUser { get; init; }
    public DateTime? PinnedAtUtc { get; init; }
}

public sealed record ChatMessageDeletedDTO
{
    public long MessageId { get; init; }
    public long ConversationId { get; init; }
    public Guid DeletedByUserId { get; init; }
    public ChatMessageDeleteScope Scope { get; init; }
    public DateTime DeletedAtUtc { get; init; }
}

public sealed record ChatMessagePinChangedDTO
{
    public long MessageId { get; init; }
    public long ConversationId { get; init; }
    public Guid UserId { get; init; }
    public bool IsPinned { get; init; }
    public DateTime? PinnedAtUtc { get; init; }
}

public sealed record ChatConversationDTO
{
    public long Id { get; init; }
    public ChatUserDTO OtherUser { get; init; } = new();
    public ChatRequestStatus RequestStatus { get; init; }
    public bool IsIncomingRequest { get; init; }
    public bool CanSendMessage { get; init; }
    public int UnreadCount { get; init; }
    public ChatMessageDTO? LastMessage { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed record ChatMessagePageDTO
{
    public IReadOnlyList<ChatMessageDTO> Items { get; init; } = [];
    public bool HasMore { get; init; }
    public long? NextBeforeMessageId { get; init; }
}

public sealed record ChatMediaUploadDTO
{
    public Stream Stream { get; init; } = Stream.Null;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long Length { get; init; }
}

public sealed record ChatSendMessageDTO
{
    public Guid RecipientUserId { get; init; }
    public string? Text { get; init; }
    public ChatMediaUploadDTO? Media { get; init; }
    public CatalogSource? GameSource { get; init; }
    public long? GameId { get; init; }
    public string? GameName { get; init; }
    public string? GameCoverUrl { get; init; }
}

public sealed record ChatReadReceiptDTO
{
    public long ConversationId { get; init; }
    public Guid ReadByUserId { get; init; }
    public DateTime ReadAtUtc { get; init; }
    public IReadOnlyList<long> MessageIds { get; init; } = [];
}

public sealed record ChatRequestDecisionDTO
{
    public long ConversationId { get; init; }
    public ChatRequestStatus Status { get; init; }
    public DateTime RespondedAtUtc { get; init; }
}

public sealed record StoredChatMediaDTO
{
    public Stream Content { get; init; } = Stream.Null;
    public string ContentType { get; init; } = "application/octet-stream";
    public string FileName { get; init; } = string.Empty;
}
