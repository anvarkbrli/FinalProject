using System.Data;
using Ludora.Application.DTOs.Chat;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class ChatService : IChatService
{
    private const int MaxMessageLength = 4000;
    private const int MaxPageSize = 100;
    private readonly AppDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IBadgeEvaluationQueue _badgeQueue;

    public ChatService(
        AppDbContext context,
        IFileStorageService fileStorage,
        IBadgeEvaluationQueue badgeQueue)
    {
        _context = context;
        _fileStorage = fileStorage;
        _badgeQueue = badgeQueue;
    }

    public async Task<IReadOnlyList<ChatConversationDTO>> GetConversationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rows = await BuildConversationQuery(userId)
            .Where(x =>
                x.RequestStatus == ChatRequestStatus.Accepted ||
                (x.RequestStatus == ChatRequestStatus.Pending &&
                 x.InitiatedByUserId == userId))
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        return rows.Select(x => MapConversation(x, userId)).ToList();
    }

    public async Task<IReadOnlyList<ChatConversationDTO>> GetMessageRequestsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rows = await BuildConversationQuery(userId)
            .Where(x =>
                x.RequestStatus == ChatRequestStatus.Pending &&
                x.InitiatedByUserId != userId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return rows.Select(x => MapConversation(x, userId)).ToList();
    }

    public async Task<ChatMessagePageDTO> GetMessagesAsync(
        Guid userId,
        long conversationId,
        long? beforeMessageId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageSize is < 1 or > MaxPageSize)
            throw new BadRequestException($"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");

        await EnsureParticipantAsync(userId, conversationId, cancellationToken);

        var query = _context.ChatMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId);

        if (beforeMessageId.HasValue)
            query = query.Where(x => x.Id < beforeMessageId.Value);

        var rows = await query
            .OrderByDescending(x => x.Id)
            .Take(pageSize + 1)
            .Select(x => new MessageRow(
                x.Id,
                x.ConversationId,
                x.SenderUserId,
                x.Kind,
                x.Text,
                x.MediaStorageKey,
                x.MediaContentType,
                x.GameSource,
                x.GameId,
                x.GameName,
                x.GameCoverUrl,
                x.SentAtUtc,
                x.DeliveredAtUtc,
                x.ReadAtUtc))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var items = rows
            .Take(pageSize)
            .Reverse()
            .Select(MapMessage)
            .ToList();

        return new ChatMessagePageDTO
        {
            Items = items,
            HasMore = hasMore,
            NextBeforeMessageId = hasMore && items.Count > 0
                ? items[0].Id
                : null
        };
    }

    public async Task<ChatMessageDTO> SendMessageAsync(
        Guid senderUserId,
        ChatSendMessageDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (senderUserId == Guid.Empty ||
            dto.RecipientUserId == Guid.Empty ||
            senderUserId == dto.RecipientUserId)
            throw new BadRequestException("Mesaj alıcısı düzgün deyil.");

        var text = string.IsNullOrWhiteSpace(dto.Text) ? null : dto.Text.Trim();
        if (text?.Length > MaxMessageLength)
            throw new BadRequestException($"Mesaj maksimum {MaxMessageLength} simvol ola bilər.");

        var hasGame = dto.GameSource.HasValue || dto.GameId.HasValue;
        if (hasGame &&
            (!dto.GameSource.HasValue ||
             !dto.GameId.HasValue ||
             dto.GameId <= 0 ||
             string.IsNullOrWhiteSpace(dto.GameName)))
        {
            throw new BadRequestException("Oyun tövsiyəsi məlumatları tam deyil.");
        }

        if (text is null && dto.Media is null && !hasGame)
            throw new BadRequestException("Boş mesaj göndərilə bilməz.");

        var recipient = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == dto.RecipientUserId)
            .Select(x => new { x.Id, x.IsPrivate })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Mesaj alıcısı tapılmadı.");

        var isBlocked = await _context.UserBlocks
            .AsNoTracking()
            .AnyAsync(x =>
                    (x.BlockerId == senderUserId &&
                     x.BlockedUserId == dto.RecipientUserId) ||
                    (x.BlockerId == dto.RecipientUserId &&
                     x.BlockedUserId == senderUserId),
                cancellationToken);

        if (isBlocked)
            throw new ForbiddenException("Bloklanmış istifadəçi ilə mesajlaşmaq mümkün deyil.");

        var senderIsFollower = await _context.UserFollows
            .AsNoTracking()
            .AnyAsync(x =>
                    x.FollowerId == senderUserId &&
                    x.FollowingId == dto.RecipientUserId &&
                    x.Status == FollowStatus.Accepted,
                cancellationToken);

        string? savedMediaKey = null;
        string? mediaContentType = null;
        long? mediaLength = null;
        if (dto.Media is not null)
        {
            var stored = await _fileStorage.SaveChatMediaAsync(
                dto.Media,
                cancellationToken);
            savedMediaKey = stored.StorageKey;
            mediaContentType = stored.ContentType;
            mediaLength = stored.Length;
        }

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var (userOneId, userTwoId) = NormalizePair(senderUserId, dto.RecipientUserId);
            var conversation = await _context.ChatConversations
                .FirstOrDefaultAsync(
                    x => x.UserOneId == userOneId && x.UserTwoId == userTwoId,
                    cancellationToken);

            if (conversation is null)
            {
                var requiresRequest = recipient.IsPrivate && !senderIsFollower;
                conversation = new ChatConversation
                {
                    UserOneId = userOneId,
                    UserTwoId = userTwoId,
                    InitiatedByUserId = senderUserId,
                    RequestStatus = requiresRequest
                        ? ChatRequestStatus.Pending
                        : ChatRequestStatus.Accepted,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _context.ChatConversations.Add(conversation);
            }
            else
            {
                if (conversation.RequestStatus == ChatRequestStatus.Rejected)
                    throw new ForbiddenException("Bu mesaj istəyi rədd edilib.");

                if (conversation.RequestStatus == ChatRequestStatus.Pending)
                {
                    if (conversation.InitiatedByUserId != senderUserId)
                        throw new ForbiddenException("Cavab yazmaq üçün əvvəlcə mesaj istəyini qəbul et.");

                    var alreadySent = await _context.ChatMessages.AnyAsync(
                        x => x.ConversationId == conversation.Id,
                        cancellationToken);
                    if (alreadySent)
                    {
                        throw new ForbiddenException(
                            "Gizli profili izləmədən yalnız bir mesaj istəyi göndərə bilərsən.");
                    }
                }
            }

            var now = DateTime.UtcNow;
            var kind = hasGame
                ? ChatMessageKind.GameRecommendation
                : mediaContentType?.StartsWith("video/", StringComparison.Ordinal) == true
                    ? ChatMessageKind.Video
                    : mediaContentType is not null
                        ? ChatMessageKind.Image
                        : ChatMessageKind.Text;

            var message = new ChatMessage
            {
                Conversation = conversation,
                SenderUserId = senderUserId,
                Kind = kind,
                Text = text,
                MediaStorageKey = savedMediaKey,
                MediaContentType = mediaContentType,
                MediaLength = mediaLength,
                GameSource = hasGame ? dto.GameSource : null,
                GameId = hasGame ? dto.GameId : null,
                GameName = hasGame ? NormalizeOptional(dto.GameName, 300) : null,
                GameCoverUrl = hasGame ? NormalizeGameCoverUrl(dto.GameCoverUrl) : null,
                SentAtUtc = now
            };

            conversation.UpdatedAtUtc = now;
            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _badgeQueue.Enqueue(senderUserId);
            return MapMessage(new MessageRow(
                message.Id,
                message.ConversationId,
                message.SenderUserId,
                message.Kind,
                message.Text,
                message.MediaStorageKey,
                message.MediaContentType,
                message.GameSource,
                message.GameId,
                message.GameName,
                message.GameCoverUrl,
                message.SentAtUtc,
                message.DeliveredAtUtc,
                message.ReadAtUtc));
        }
        catch
        {
            if (savedMediaKey is not null)
                await _fileStorage.DeleteChatMediaAsync(savedMediaKey);
            throw;
        }
    }

    public Task<ChatRequestDecisionDTO> AcceptRequestAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default) =>
        DecideRequestAsync(userId, conversationId, ChatRequestStatus.Accepted, cancellationToken);

    public Task<ChatRequestDecisionDTO> RejectRequestAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default) =>
        DecideRequestAsync(userId, conversationId, ChatRequestStatus.Rejected, cancellationToken);

    public async Task<ChatReadReceiptDTO> MarkReadAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureParticipantAsync(userId, conversationId, cancellationToken);
        var now = DateTime.UtcNow;
        var messageIds = await _context.ChatMessages
            .Where(x =>
                x.ConversationId == conversationId &&
                x.SenderUserId != userId &&
                x.ReadAtUtc == null)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (messageIds.Count > 0)
        {
            await _context.ChatMessages
                .Where(x => messageIds.Contains(x.Id))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.DeliveredAtUtc, x => x.DeliveredAtUtc ?? now)
                        .SetProperty(x => x.ReadAtUtc, now),
                    cancellationToken);
        }

        return new ChatReadReceiptDTO
        {
            ConversationId = conversationId,
            ReadByUserId = userId,
            ReadAtUtc = now,
            MessageIds = messageIds
        };
    }

    public async Task<IReadOnlyList<ChatReadReceiptDTO>> MarkDeliveredAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var rows = await _context.ChatMessages
            .Where(x =>
                x.SenderUserId != userId &&
                x.DeliveredAtUtc == null &&
                (x.Conversation.UserOneId == userId ||
                 x.Conversation.UserTwoId == userId))
            .Select(x => new { x.Id, x.ConversationId })
            .Take(1000)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return [];

        var ids = rows.Select(x => x.Id).ToList();
        await _context.ChatMessages
            .Where(x => ids.Contains(x.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.DeliveredAtUtc, now),
                cancellationToken);

        return rows
            .GroupBy(x => x.ConversationId)
            .Select(group => new ChatReadReceiptDTO
            {
                ConversationId = group.Key,
                ReadByUserId = userId,
                ReadAtUtc = now,
                MessageIds = group.Select(x => x.Id).ToList()
            })
            .ToList();
    }

    public async Task<StoredChatMediaDTO> OpenMediaAsync(
        Guid userId,
        long messageId,
        CancellationToken cancellationToken = default)
    {
        var media = await _context.ChatMessages
            .AsNoTracking()
            .Where(x =>
                x.Id == messageId &&
                (x.Conversation.UserOneId == userId ||
                 x.Conversation.UserTwoId == userId))
            .Select(x => x.MediaStorageKey)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(media))
            throw new NotFoundException("Mesaj mediası tapılmadı.");

        return await _fileStorage.OpenChatMediaAsync(media, cancellationToken);
    }

    private async Task<ChatRequestDecisionDTO> DecideRequestAsync(
        Guid userId,
        long conversationId,
        ChatRequestStatus status,
        CancellationToken cancellationToken)
    {
        var conversation = await _context.ChatConversations
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken)
            ?? throw new NotFoundException("Mesaj istəyi tapılmadı.");

        if (conversation.UserOneId != userId && conversation.UserTwoId != userId)
            throw new ForbiddenException("Bu mesaj istəyinə giriş yoxdur.");
        if (conversation.InitiatedByUserId == userId)
            throw new ForbiddenException("Öz mesaj istəyini cavablandıra bilməzsən.");
        if (conversation.RequestStatus != ChatRequestStatus.Pending)
            throw new ConflictException("Mesaj istəyi artıq cavablandırılıb.");

        var now = DateTime.UtcNow;
        conversation.RequestStatus = status;
        conversation.RespondedAtUtc = now;
        conversation.UpdatedAtUtc = now;
        await _context.SaveChangesAsync(cancellationToken);

        return new ChatRequestDecisionDTO
        {
            ConversationId = conversationId,
            Status = status,
            RespondedAtUtc = now
        };
    }

    private IQueryable<ConversationRow> BuildConversationQuery(Guid userId)
    {
        return _context.ChatConversations
            .AsNoTracking()
            .Where(x => x.UserOneId == userId || x.UserTwoId == userId)
            .Select(x => new ConversationRow
            {
                Id = x.Id,
                UserOneId = x.UserOneId,
                UserTwoId = x.UserTwoId,
                InitiatedByUserId = x.InitiatedByUserId,
                RequestStatus = x.RequestStatus,
                UpdatedAtUtc = x.UpdatedAtUtc,
                OtherUserId = x.UserOneId == userId ? x.UserTwoId : x.UserOneId,
                OtherUserName = x.UserOneId == userId
                    ? x.UserTwo.UserName!
                    : x.UserOne.UserName!,
                OtherUserImage = x.UserOneId == userId
                    ? x.UserTwo.ProfileImageUrl
                    : x.UserOne.ProfileImageUrl,
                UnreadCount = x.Messages.Count(m =>
                    m.SenderUserId != userId && m.ReadAtUtc == null),
                MessageCount = x.Messages.Count,
                LastMessage = x.Messages
                    .OrderByDescending(m => m.Id)
                    .Select(m => new MessageRow(
                        m.Id,
                        m.ConversationId,
                        m.SenderUserId,
                        m.Kind,
                        m.Text,
                        m.MediaStorageKey,
                        m.MediaContentType,
                        m.GameSource,
                        m.GameId,
                        m.GameName,
                        m.GameCoverUrl,
                        m.SentAtUtc,
                        m.DeliveredAtUtc,
                        m.ReadAtUtc))
                    .FirstOrDefault()
            });
    }

    private async Task EnsureParticipantAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken)
    {
        var exists = await _context.ChatConversations
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == conversationId &&
                     (x.UserOneId == userId || x.UserTwoId == userId) &&
                     x.RequestStatus != ChatRequestStatus.Rejected,
                cancellationToken);

        if (!exists)
            throw new NotFoundException("Söhbət tapılmadı.");
    }

    private static ChatConversationDTO MapConversation(
        ConversationRow row,
        Guid userId) =>
        new()
        {
            Id = row.Id,
            OtherUser = new ChatUserDTO
            {
                Id = row.OtherUserId,
                UserName = row.OtherUserName,
                ProfileImageUrl = row.OtherUserImage
            },
            RequestStatus = row.RequestStatus,
            IsIncomingRequest =
                row.RequestStatus == ChatRequestStatus.Pending &&
                row.InitiatedByUserId != userId,
            CanSendMessage =
                row.RequestStatus == ChatRequestStatus.Accepted ||
                (row.RequestStatus == ChatRequestStatus.Pending &&
                 row.InitiatedByUserId == userId &&
                 row.MessageCount == 0),
            UnreadCount = row.UnreadCount,
            LastMessage = row.LastMessage is null ? null : MapMessage(row.LastMessage),
            UpdatedAtUtc = row.UpdatedAtUtc
        };

    private static ChatMessageDTO MapMessage(MessageRow row) =>
        new()
        {
            Id = row.Id,
            ConversationId = row.ConversationId,
            SenderUserId = row.SenderUserId,
            Kind = row.Kind,
            Text = row.Text,
            MediaUrl = row.MediaStorageKey is null
                ? null
                : $"/chat/messages/{row.Id}/media",
            MediaContentType = row.MediaContentType,
            GameSource = row.GameSource,
            GameId = row.GameId,
            GameName = row.GameName,
            GameCoverUrl = row.GameCoverUrl,
            GameDetailUrl = row.GameSource.HasValue && row.GameId.HasValue
                ? $"/games/{row.GameSource.Value}/{row.GameId.Value}"
                : null,
            SentAtUtc = row.SentAtUtc,
            DeliveredAtUtc = row.DeliveredAtUtc,
            ReadAtUtc = row.ReadAtUtc,
            DeliveryStatus = row.ReadAtUtc.HasValue
                ? ChatMessageDeliveryStatus.Read
                : row.DeliveredAtUtc.HasValue
                    ? ChatMessageDeliveryStatus.Delivered
                    : ChatMessageDeliveryStatus.Sent
        };

    private static (Guid UserOneId, Guid UserTwoId) NormalizePair(
        Guid left,
        Guid right) =>
        left.CompareTo(right) < 0 ? (left, right) : (right, left);

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private static string? NormalizeGameCoverUrl(string? value)
    {
        var normalized = NormalizeOptional(value, 2048);
        if (normalized is null)
            return null;
        if (normalized.StartsWith("/", StringComparison.Ordinal) &&
            !normalized.StartsWith("//", StringComparison.Ordinal))
            return normalized;

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
            throw new BadRequestException("Oyun şəkli URL-i təhlükəsiz deyil.");

        var allowedHosts = new[]
        {
            "images.igdb.com",
            "cdn.akamai.steamstatic.com",
            "shared.akamai.steamstatic.com"
        };
        if (!allowedHosts.Any(host =>
                string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith($".{host}", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BadRequestException("Oyun şəkli domeninə icazə verilmir.");
        }

        return uri.ToString();
    }

    private sealed class ConversationRow
    {
        public long Id { get; init; }
        public Guid UserOneId { get; init; }
        public Guid UserTwoId { get; init; }
        public Guid InitiatedByUserId { get; init; }
        public ChatRequestStatus RequestStatus { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
        public Guid OtherUserId { get; init; }
        public string OtherUserName { get; init; } = string.Empty;
        public string? OtherUserImage { get; init; }
        public int UnreadCount { get; init; }
        public int MessageCount { get; init; }
        public MessageRow? LastMessage { get; init; }
    }

    private sealed record MessageRow(
        long Id,
        long ConversationId,
        Guid SenderUserId,
        ChatMessageKind Kind,
        string? Text,
        string? MediaStorageKey,
        string? MediaContentType,
        CatalogSource? GameSource,
        long? GameId,
        string? GameName,
        string? GameCoverUrl,
        DateTime SentAtUtc,
        DateTime? DeliveredAtUtc,
        DateTime? ReadAtUtc);
}
