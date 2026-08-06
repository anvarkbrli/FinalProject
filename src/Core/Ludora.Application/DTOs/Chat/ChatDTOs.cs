using Ludora.Application.DTOs;
using Ludora.Domain.Enums;

namespace Ludora.Application.DTOs.Chat
{
    public sealed record ChatUserDTO
    {
        public Guid Id { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string? ProfileImageUrl { get; init; }
        public bool IsOnline { get; init; }
        public DateTime? LastSeenAtUtc { get; init; }
    }

    public sealed record ChatPresenceChangedDTO
    {
        public Guid UserId { get; init; }
        public bool IsOnline { get; init; }
        public DateTime? LastSeenAtUtc { get; init; }
    }

    public sealed record ChatReplyPreviewDTO
    {
        public long MessageId { get; init; }
        public Guid SenderUserId { get; init; }
        public string SenderUserName { get; init; } = string.Empty;
        public ChatMessageKind Kind { get; init; }
        public string? Text { get; init; }
        public string? GameName { get; init; }
        public bool IsDeleted { get; init; }
    }

    public sealed record ChatReactionSummaryDTO
    {
        public ChatReactionType ReactionType { get; init; }
        public int Count { get; init; }
        public bool ReactedByCurrentUser { get; init; }
    }

    public sealed record ChatStreakDTO
    {
        public int CurrentCount { get; init; }
        public int LongestCount { get; init; }
        public DateOnly? LastCompletedDate { get; init; }
        public bool IsCompletedToday { get; init; }
        public bool IsActive { get; init; }
    }

    public sealed record ChatStreakUpdatedDTO
    {
        public long ConversationId { get; init; }
        public ChatStreakDTO Streak { get; init; } = new();
        public bool WasIncremented { get; init; }
    }

    public sealed record ChatMessageMediaDTO
    {
        public long Id { get; init; }
        public string Url { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public long Length { get; init; }
        public int SortOrder { get; init; }
    }

    public sealed record ChatMessageDTO
    {
        public long Id { get; init; }
        public long ConversationId { get; init; }
        public Guid SenderUserId { get; init; }
        public ChatUserDTO? SenderUser { get; init; }
        public ChatMessageKind Kind { get; init; }
        public string? Text { get; init; }
        public string? MediaUrl { get; init; }
        public string? MediaContentType { get; init; }
        public IReadOnlyList<ChatMessageMediaDTO> MediaItems { get; init; } = [];
        public CatalogSource? GameSource { get; init; }
        public long? GameId { get; init; }
        public string? GameName { get; init; }
        public string? GameCoverUrl { get; init; }
        public string? GameDetailUrl { get; init; }
        public ChatReplyPreviewDTO? ReplyTo { get; init; }
        public bool IsForwarded { get; init; }
        public IReadOnlyList<ChatReactionSummaryDTO> Reactions { get; init; } = [];
        public DateTime SentAtUtc { get; init; }
        public DateTime? DeliveredAtUtc { get; init; }
        public DateTime? ReadAtUtc { get; init; }
        public ChatMessageDeliveryStatus DeliveryStatus { get; init; }
        public bool IsDeletedForEveryone { get; init; }
        public DateTime? DeletedForEveryoneAtUtc { get; init; }
        public bool IsPinnedByCurrentUser { get; init; }
        public DateTime? PinnedAtUtc { get; init; }
    }

    public sealed record ChatSendMessageResultDTO
    {
        public ChatMessageDTO Message { get; init; } = new();
        public ChatStreakUpdatedDTO? StreakUpdate { get; init; }
    }

    public sealed record ChatForwardMessageRequestDTO
    {
        public IReadOnlyList<long> TargetConversationIds { get; init; } = [];
    }

    public sealed record ChatForwardedMessageDTO
    {
        public long ConversationId { get; init; }
        public IReadOnlyList<Guid> RecipientUserIds { get; init; } = [];
        public ChatMessageDTO Message { get; init; } = new();
        public ChatStreakUpdatedDTO? StreakUpdate { get; init; }
    }

    public sealed record ChatForwardMessagesResultDTO
    {
        public IReadOnlyList<ChatForwardedMessageDTO> Items { get; init; } = [];
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

    public sealed record ChatMessageReactionChangedDTO
    {
        public long MessageId { get; init; }
        public long ConversationId { get; init; }
        public Guid UserId { get; init; }
        public ChatReactionType? ReactionType { get; init; }
        public IReadOnlyList<ChatReactionSummaryDTO> Reactions { get; init; } = [];
    }

    public sealed record ChatConversationDTO
    {
        public long Id { get; init; }
        public ChatUserDTO? OtherUser { get; init; }
        public bool IsGroup { get; init; }
        public string? GroupName { get; init; }
        public string? GroupDescription { get; init; }
        public string? GroupImageUrl { get; init; }
        public bool IsGroupCreator { get; init; }
        public int MemberCount { get; init; }
        public int OnlineMemberCount { get; init; }
        public ChatRequestStatus RequestStatus { get; init; }
        public bool IsIncomingRequest { get; init; }
        public bool CanSendMessage { get; init; }
        public bool IsArchived { get; init; }
        public int UnreadCount { get; init; }
        public ChatMessageDTO? LastMessage { get; init; }
        public ChatStreakDTO Streak { get; init; } = new();
        public DateTime UpdatedAtUtc { get; init; }
    }

    public sealed record ChatMessagePageDTO
    {
        public IReadOnlyList<ChatMessageDTO> Items { get; init; } = [];
        public bool HasMore { get; init; }
        public long? NextBeforeMessageId { get; init; }
    }

    public sealed record ChatMessageSearchPageDTO
    {
        public IReadOnlyList<ChatMessageDTO> Items { get; init; } = [];
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public bool HasMore { get; init; }
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
        public Guid? RecipientUserId { get; init; }
        public long? ConversationId { get; init; }
        public string? Text { get; init; }
        public IReadOnlyList<ChatMediaUploadDTO> Media { get; init; } = [];
        public CatalogSource? GameSource { get; init; }
        public long? GameId { get; init; }
        public string? GameName { get; init; }
        public string? GameCoverUrl { get; init; }
        public long? ReplyToMessageId { get; init; }
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

    public sealed record ChatConversationArchiveChangedDTO
    {
        public long ConversationId { get; init; }
        public Guid UserId { get; init; }
        public bool IsArchived { get; init; }
        public DateTime? ArchivedAtUtc { get; init; }
    }

    public sealed record ChatConversationDeletedDTO
    {
        public long ConversationId { get; init; }
        public Guid UserId { get; init; }
        public int DeletedMessageCount { get; init; }
        public DateTime DeletedAtUtc { get; init; }
    }

    public sealed record ChatConversationParticipantsDTO
    {
        public long ConversationId { get; init; }
        public Guid? UserOneId { get; init; }
        public Guid? UserTwoId { get; init; }
        public IReadOnlyList<Guid> UserIds { get; init; } = [];

        public Guid GetOtherUserId(Guid currentUserId)
        {
            var other = UserIds.FirstOrDefault(id => id != currentUserId);
            return other == Guid.Empty
                ? throw new InvalidOperationException("Söhbətin digər iştirakçısı tapılmadı.")
                : other;
        }
    }

    public sealed record ChatTypingDTO
    {
        public long ConversationId { get; init; }
        public Guid UserId { get; init; }
        public bool IsTyping { get; init; }
        public DateTime ExpiresAtUtc { get; init; }
    }

    public sealed record ChatGroupMemberDTO
    {
        public Guid Id { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string? ProfileImageUrl { get; init; }
        public bool IsCreator { get; init; }
        public DateTime JoinedAtUtc { get; init; }
    }

    public sealed record ChatGroupDTO
    {
        public long ConversationId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? ImageUrl { get; init; }
        public Guid CreatorUserId { get; init; }
        public bool IsCurrentUserCreator { get; init; }
        public IReadOnlyList<ChatGroupMemberDTO> Members { get; init; } = [];
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }

    public sealed record ChatGroupCreateDTO
    {
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public IReadOnlyList<Guid> MemberUserIds { get; init; } = [];
        public ProfileImageUploadDTO? Image { get; init; }
    }

    public sealed record ChatGroupUpdateDTO
    {
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public ProfileImageUploadDTO? Image { get; init; }
        public bool RemoveImage { get; init; }
        public IReadOnlyList<Guid> MemberUserIdsToAdd { get; init; } = [];
    }

    public sealed record ChatGroupMembersUpdateDTO
    {
        public IReadOnlyList<Guid> UserIds { get; init; } = [];
    }

    public sealed record ChatGroupChangedDTO
    {
        public long ConversationId { get; init; }
        public string ChangeType { get; init; } = string.Empty;
        public IReadOnlyList<Guid> ParticipantUserIds { get; init; } = [];
        public Guid? AffectedUserId { get; init; }
        public Guid? NewCreatorUserId { get; init; }
        public bool GroupDeleted { get; init; }
        public ChatGroupDTO? Group { get; init; }
    }

    public sealed record ChatBulkDeleteRequestDTO
    {
        public IReadOnlyList<long> MessageIds { get; init; } = [];
        public ChatMessageDeleteScope Scope { get; init; } = ChatMessageDeleteScope.ForMe;
    }

    public sealed record ChatBulkDeleteResultDTO
    {
        public long ConversationId { get; init; }
        public ChatMessageDeleteScope Scope { get; init; }
        public IReadOnlyList<ChatMessageDeletedDTO> Items { get; init; } = [];
    }

    public sealed record StoredChatMediaDTO
    {
        public Stream Content { get; init; } = Stream.Null;
        public string ContentType { get; init; } = "application/octet-stream";
        public string FileName { get; init; } = string.Empty;
    }
}
