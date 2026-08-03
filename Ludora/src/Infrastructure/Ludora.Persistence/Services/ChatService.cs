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
    private const int MaxSearchPageSize = 50;
    private const int MaxMediaCount = 10;
    private const long MaxTotalMediaBytes = 150L * 1024 * 1024;
    private static readonly TimeZoneInfo BakuTimeZone = ResolveBakuTimeZone();

    private readonly AppDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IBadgeEvaluationQueue _badgeQueue;
    private readonly IUserPresenceTracker _presenceTracker;

    public ChatService(
        AppDbContext context,
        IFileStorageService fileStorage,
        IBadgeEvaluationQueue badgeQueue,
        IUserPresenceTracker presenceTracker)
    {
        _context = context;
        _fileStorage = fileStorage;
        _badgeQueue = badgeQueue;
        _presenceTracker = presenceTracker;
    }

    public async Task<IReadOnlyList<ChatConversationDTO>> GetConversationsAsync(
        Guid userId,
        bool archived = false,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = NormalizeConversationSearch(search);
        var rows = await _context.ChatConversations
            .AsNoTracking()
            .Where(x =>
                (x.UserOneId == userId || x.UserTwoId == userId) &&
                (x.RequestStatus == ChatRequestStatus.Accepted ||
                 (x.RequestStatus == ChatRequestStatus.Pending &&
                  x.InitiatedByUserId == userId)) &&
                x.Messages.Any(message =>
                    !message.UserStates.Any(state =>
                        state.UserId == userId && state.IsDeleted)) &&
                (archived
                    ? x.UserStates.Any(state =>
                        state.UserId == userId && state.IsArchived)
                    : !x.UserStates.Any(state =>
                        state.UserId == userId && state.IsArchived)) &&
                (normalizedSearch == null ||
                 (x.UserOneId == userId
                    ? x.UserTwo.UserName!.Contains(normalizedSearch)
                    : x.UserOne.UserName!.Contains(normalizedSearch))))
            .OrderByDescending(x => archived
                ? x.UserStates
                    .Where(state => state.UserId == userId)
                    .Select(state => state.ArchivedAtUtc)
                    .FirstOrDefault()
                : x.UpdatedAtUtc)
            .Take(200)
            .Select(x => new ConversationRow
            {
                Id = x.Id,
                UserOneId = x.UserOneId,
                UserTwoId = x.UserTwoId,
                InitiatedByUserId = x.InitiatedByUserId,
                RequestStatus = x.RequestStatus,
                UpdatedAtUtc = x.UpdatedAtUtc,
                CurrentStreak = x.CurrentStreak,
                LongestStreak = x.LongestStreak,
                LastCompletedStreakDate = x.LastCompletedStreakDate,
                IsArchived = x.UserStates.Any(state =>
                    state.UserId == userId && state.IsArchived),
                OtherUserId = x.UserOneId == userId ? x.UserTwoId : x.UserOneId,
                OtherUserName = x.UserOneId == userId
                    ? x.UserTwo.UserName!
                    : x.UserOne.UserName!,
                OtherUserImage = x.UserOneId == userId
                    ? x.UserTwo.ProfileImageUrl
                    : x.UserOne.ProfileImageUrl,
                OtherUserLastSeenAtUtc = x.UserOneId == userId
                    ? x.UserTwo.RefreshSessions
                        .OrderByDescending(session => session.LastActiveAtUtc)
                        .Select(session => (DateTime?)session.LastActiveAtUtc)
                        .FirstOrDefault()
                    : x.UserOne.RefreshSessions
                        .OrderByDescending(session => session.LastActiveAtUtc)
                        .Select(session => (DateTime?)session.LastActiveAtUtc)
                        .FirstOrDefault(),
                UnreadCount = x.Messages.Count(message =>
                    message.SenderUserId != userId &&
                    message.ReadAtUtc == null &&
                    message.DeletedForEveryoneAtUtc == null &&
                    !message.UserStates.Any(state =>
                        state.UserId == userId && state.IsDeleted)),
                MessageCount = x.Messages.Count,
                LastMessageId = x.Messages
                    .Where(message => !message.UserStates.Any(state =>
                        state.UserId == userId && state.IsDeleted))
                    .OrderByDescending(message => message.Id)
                    .Select(message => (long?)message.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var lastMessageIds = rows
            .Where(x => x.LastMessageId.HasValue)
            .Select(x => x.LastMessageId!.Value)
            .Distinct()
            .ToList();
        var lastMessages = await LoadMessagesByIdsAsync(
            userId,
            lastMessageIds,
            cancellationToken);

        return rows
            .Select(row => MapConversation(
                row,
                userId,
                row.LastMessageId.HasValue &&
                lastMessages.TryGetValue(row.LastMessageId.Value, out var message)
                    ? message
                    : null))
            .ToList();
    }

    public async Task<IReadOnlyList<ChatConversationDTO>> GetMessageRequestsAsync(
        Guid userId,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = NormalizeConversationSearch(search);
        var rows = await _context.ChatConversations
            .AsNoTracking()
            .Where(x =>
                (x.UserOneId == userId || x.UserTwoId == userId) &&
                x.RequestStatus == ChatRequestStatus.Pending &&
                x.InitiatedByUserId != userId &&
                x.Messages.Any(message =>
                    !message.UserStates.Any(state =>
                        state.UserId == userId && state.IsDeleted)) &&
                (normalizedSearch == null ||
                 (x.UserOneId == userId
                    ? x.UserTwo.UserName!.Contains(normalizedSearch)
                    : x.UserOne.UserName!.Contains(normalizedSearch))))
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(100)
            .Select(x => new ConversationRow
            {
                Id = x.Id,
                UserOneId = x.UserOneId,
                UserTwoId = x.UserTwoId,
                InitiatedByUserId = x.InitiatedByUserId,
                RequestStatus = x.RequestStatus,
                UpdatedAtUtc = x.UpdatedAtUtc,
                CurrentStreak = x.CurrentStreak,
                LongestStreak = x.LongestStreak,
                LastCompletedStreakDate = x.LastCompletedStreakDate,
                IsArchived = false,
                OtherUserId = x.UserOneId == userId ? x.UserTwoId : x.UserOneId,
                OtherUserName = x.UserOneId == userId
                    ? x.UserTwo.UserName!
                    : x.UserOne.UserName!,
                OtherUserImage = x.UserOneId == userId
                    ? x.UserTwo.ProfileImageUrl
                    : x.UserOne.ProfileImageUrl,
                OtherUserLastSeenAtUtc = x.UserOneId == userId
                    ? x.UserTwo.RefreshSessions
                        .OrderByDescending(session => session.LastActiveAtUtc)
                        .Select(session => (DateTime?)session.LastActiveAtUtc)
                        .FirstOrDefault()
                    : x.UserOne.RefreshSessions
                        .OrderByDescending(session => session.LastActiveAtUtc)
                        .Select(session => (DateTime?)session.LastActiveAtUtc)
                        .FirstOrDefault(),
                UnreadCount = x.Messages.Count(message =>
                    message.SenderUserId != userId &&
                    message.ReadAtUtc == null &&
                    message.DeletedForEveryoneAtUtc == null &&
                    !message.UserStates.Any(state =>
                        state.UserId == userId && state.IsDeleted)),
                MessageCount = x.Messages.Count,
                LastMessageId = x.Messages
                    .Where(message => !message.UserStates.Any(state =>
                        state.UserId == userId && state.IsDeleted))
                    .OrderByDescending(message => message.Id)
                    .Select(message => (long?)message.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var lastMessageIds = rows
            .Where(x => x.LastMessageId.HasValue)
            .Select(x => x.LastMessageId!.Value)
            .Distinct()
            .ToList();
        var lastMessages = await LoadMessagesByIdsAsync(
            userId,
            lastMessageIds,
            cancellationToken);

        return rows
            .Select(row => MapConversation(
                row,
                userId,
                row.LastMessageId.HasValue &&
                lastMessages.TryGetValue(row.LastMessageId.Value, out var message)
                    ? message
                    : null))
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetConversationPartnerIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ChatConversations
            .AsNoTracking()
            .Where(conversation =>
                (conversation.UserOneId == userId || conversation.UserTwoId == userId) &&
                conversation.RequestStatus != ChatRequestStatus.Rejected)
            .Select(conversation =>
                conversation.UserOneId == userId
                    ? conversation.UserTwoId
                    : conversation.UserOneId)
            .Distinct()
            .ToListAsync(cancellationToken);
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

        var query = MessageGraphQuery(userId)
            .Where(x =>
                x.ConversationId == conversationId &&
                !x.UserStates.Any(state =>
                    state.UserId == userId && state.IsDeleted));

        if (beforeMessageId.HasValue)
            query = query.Where(x => x.Id < beforeMessageId.Value);

        var rows = await query
            .OrderByDescending(x => x.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var items = rows
            .Take(pageSize)
            .Reverse()
            .Select(message => MapMessage(message, userId))
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

    public async Task<ChatMessageDTO> GetMessageAsync(
        Guid userId,
        long conversationId,
        long messageId,
        CancellationToken cancellationToken = default)
    {
        await EnsureParticipantAsync(userId, conversationId, cancellationToken);

        var message = await MessageGraphQuery(userId)
            .FirstOrDefaultAsync(x =>
                    x.Id == messageId &&
                    x.ConversationId == conversationId &&
                    !x.UserStates.Any(state =>
                        state.UserId == userId && state.IsDeleted),
                cancellationToken)
            ?? throw new NotFoundException("Mesaj tapılmadı.");

        return MapMessage(message, userId);
    }

    public async Task<ChatMessageSearchPageDTO> SearchMessagesAsync(
        Guid userId,
        long conversationId,
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await EnsureParticipantAsync(userId, conversationId, cancellationToken);

        var normalized = query?.Trim() ?? string.Empty;
        if (normalized.Length < 2)
            throw new BadRequestException("Axtarış üçün ən azı 2 simvol yazılmalıdır.");
        if (normalized.Length > 100)
            throw new BadRequestException("Axtarış mətni maksimum 100 simvol ola bilər.");
        if (page < 1)
            throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");
        if (pageSize is < 1 or > MaxSearchPageSize)
            throw new BadRequestException($"Səhifə ölçüsü 1-{MaxSearchPageSize} aralığında olmalıdır.");

        var pattern = $"%{EscapeLikePattern(normalized)}%";
        var baseQuery = _context.ChatMessages
            .AsNoTracking()
            .Where(x =>
                x.ConversationId == conversationId &&
                x.DeletedForEveryoneAtUtc == null &&
                !x.UserStates.Any(state =>
                    state.UserId == userId && state.IsDeleted) &&
                ((x.Text != null && EF.Functions.Like(x.Text, pattern, "\\")) ||
                 (x.GameName != null && EF.Functions.Like(x.GameName, pattern, "\\"))));

        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var ids = await baseQuery
            .OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var messageMap = await LoadMessagesByIdsAsync(userId, ids, cancellationToken);
        var items = ids
            .Where(messageMap.ContainsKey)
            .Select(id => messageMap[id])
            .ToList();

        return new ChatMessageSearchPageDTO
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            HasMore = page * pageSize < totalCount
        };
    }

    public async Task<ChatSendMessageResultDTO> SendMessageAsync(
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

        var mediaUploads = dto.Media ?? [];
        if (mediaUploads.Count > MaxMediaCount)
            throw new BadRequestException($"Bir mesajda maksimum {MaxMediaCount} media faylı göndərilə bilər.");
        if (mediaUploads.Sum(item => item.Length) > MaxTotalMediaBytes)
            throw new BadRequestException("Bir mesajdakı media fayllarının ümumi ölçüsü maksimum 150 MB ola bilər.");

        if (text is null && mediaUploads.Count == 0 && !hasGame)
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

        var savedMedia = new List<(string StorageKey, string ContentType, long Length, string FileName)>();
        try
        {
            foreach (var upload in mediaUploads)
            {
                var stored = await _fileStorage.SaveChatMediaAsync(
                    upload,
                    cancellationToken);
                savedMedia.Add((
                    stored.StorageKey,
                    stored.ContentType,
                    stored.Length,
                    NormalizeMediaFileName(upload.FileName)));
            }

            var firstMedia = savedMedia.FirstOrDefault();
            var hasSavedMedia = savedMedia.Count > 0;

            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var (userOneId, userTwoId) = NormalizePair(senderUserId, dto.RecipientUserId);
            var conversation = await _context.ChatConversations
                .Include(x => x.UserStates)
                .FirstOrDefaultAsync(
                    x => x.UserOneId == userOneId && x.UserTwoId == userTwoId,
                    cancellationToken);

            if (conversation is null)
            {
                if (dto.ReplyToMessageId.HasValue)
                    throw new BadRequestException("Yeni söhbətdə cavab mesajı seçilə bilməz.");

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

            if (dto.ReplyToMessageId.HasValue)
            {
                var replyExists = await _context.ChatMessages
                    .AsNoTracking()
                    .AnyAsync(x =>
                            x.Id == dto.ReplyToMessageId.Value &&
                            x.ConversationId == conversation.Id &&
                            x.DeletedForEveryoneAtUtc == null &&
                            !x.UserStates.Any(state =>
                                state.UserId == senderUserId && state.IsDeleted),
                        cancellationToken);
                if (!replyExists)
                    throw new NotFoundException("Cavab verilən mesaj tapılmadı.");
            }

            var now = DateTime.UtcNow;
            var kind = hasGame
                ? ChatMessageKind.GameRecommendation
                : hasSavedMedia && firstMedia.ContentType.StartsWith("video/", StringComparison.Ordinal)
                    ? ChatMessageKind.Video
                    : hasSavedMedia
                        ? ChatMessageKind.Image
                        : ChatMessageKind.Text;

            var message = new ChatMessage
            {
                Conversation = conversation,
                SenderUserId = senderUserId,
                ReplyToMessageId = dto.ReplyToMessageId,
                Kind = kind,
                Text = text,
                MediaStorageKey = hasSavedMedia ? firstMedia.StorageKey : null,
                MediaContentType = hasSavedMedia ? firstMedia.ContentType : null,
                MediaLength = hasSavedMedia ? firstMedia.Length : null,
                MediaItems = savedMedia
                    .Select((item, index) => new ChatMessageMedia
                    {
                        StorageKey = item.StorageKey,
                        ContentType = item.ContentType,
                        OriginalFileName = item.FileName,
                        Length = item.Length,
                        SortOrder = index
                    })
                    .ToList(),
                GameSource = hasGame ? dto.GameSource : null,
                GameId = hasGame ? dto.GameId : null,
                GameName = hasGame ? NormalizeOptional(dto.GameName, 300) : null,
                GameCoverUrl = hasGame ? NormalizeGameCoverUrl(dto.GameCoverUrl) : null,
                SentAtUtc = now
            };

            conversation.UpdatedAtUtc = now;
            foreach (var state in conversation.UserStates.Where(x => x.IsArchived))
            {
                state.IsArchived = false;
                state.ArchivedAtUtc = null;
            }

            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync(cancellationToken);

            ChatStreakUpdatedDTO? streakUpdate = null;
            if (conversation.RequestStatus == ChatRequestStatus.Accepted)
            {
                streakUpdate = await UpdateStreakAfterMessageAsync(
                    conversation,
                    cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var loadedMessage = await MessageGraphQuery(senderUserId)
                .FirstAsync(x => x.Id == message.Id, cancellationToken);

            _badgeQueue.Enqueue(senderUserId);
            return new ChatSendMessageResultDTO
            {
                Message = MapMessage(loadedMessage, senderUserId),
                StreakUpdate = streakUpdate
            };
        }
        catch
        {
            foreach (var item in savedMedia)
                await _fileStorage.DeleteChatMediaAsync(item.StorageKey);
            throw;
        }
    }

    public async Task<ChatForwardMessagesResultDTO> ForwardMessageAsync(
        Guid userId,
        long sourceMessageId,
        IReadOnlyCollection<long> targetConversationIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetConversationIds);
        if (userId == Guid.Empty || sourceMessageId <= 0)
            throw new BadRequestException("Yönləndiriləcək mesaj düzgün deyil.");

        var targetIds = targetConversationIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (targetIds.Count == 0)
            throw new BadRequestException("Ən azı bir söhbət seçilməlidir.");
        if (targetIds.Count > 20)
            throw new BadRequestException("Bir mesaj eyni anda maksimum 20 söhbətə yönləndirilə bilər.");

        var source = await _context.ChatMessages
            .AsNoTracking()
            .Include(message => message.Conversation)
            .Include(message => message.UserStates)
            .Include(message => message.MediaItems)
            .FirstOrDefaultAsync(message => message.Id == sourceMessageId, cancellationToken)
            ?? throw new NotFoundException("Yönləndiriləcək mesaj tapılmadı.");

        if (source.Conversation.UserOneId != userId &&
            source.Conversation.UserTwoId != userId)
        {
            throw new ForbiddenException("Bu mesaja giriş yoxdur.");
        }
        if (source.DeletedForEveryoneAtUtc.HasValue ||
            source.UserStates.Any(state => state.UserId == userId && state.IsDeleted))
        {
            throw new NotFoundException("Yönləndiriləcək mesaj artıq əlçatan deyil.");
        }
        if (targetIds.Contains(source.ConversationId))
            throw new BadRequestException("Mesaj eyni söhbətə yönləndirilə bilməz.");

        var targets = await _context.ChatConversations
            .Include(conversation => conversation.UserStates)
            .Where(conversation => targetIds.Contains(conversation.Id))
            .ToListAsync(cancellationToken);

        if (targets.Count != targetIds.Count)
            throw new NotFoundException("Seçilmiş söhbətlərdən biri tapılmadı.");

        foreach (var target in targets)
        {
            if (target.UserOneId != userId && target.UserTwoId != userId)
                throw new ForbiddenException("Seçilmiş söhbətlərdən birinə giriş yoxdur.");
            if (target.RequestStatus != ChatRequestStatus.Accepted)
                throw new ConflictException("Mesaj yalnız qəbul edilmiş söhbətlərə yönləndirilə bilər.");
        }

        var recipientIds = targets
            .Select(target => target.UserOneId == userId
                ? target.UserTwoId
                : target.UserOneId)
            .Distinct()
            .ToList();
        var hasBlockedConversation = await _context.UserBlocks
            .AsNoTracking()
            .AnyAsync(block =>
                    (recipientIds.Contains(block.BlockerId) && block.BlockedUserId == userId) ||
                    (block.BlockerId == userId && recipientIds.Contains(block.BlockedUserId)),
                cancellationToken);
        if (hasBlockedConversation)
            throw new ForbiddenException("Bloklanmış istifadəçi olan söhbətə mesaj yönləndirilə bilməz.");

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var now = DateTime.UtcNow;
        var created = new List<(ChatConversation Conversation, Guid RecipientId, ChatMessage Message)>();
        foreach (var target in targets)
        {
            var recipientId = target.UserOneId == userId
                ? target.UserTwoId
                : target.UserOneId;
            var forwardedMessage = new ChatMessage
            {
                Conversation = target,
                SenderUserId = userId,
                IsForwarded = true,
                Kind = source.Kind,
                Text = source.Text,
                MediaStorageKey = source.MediaStorageKey,
                MediaContentType = source.MediaContentType,
                MediaLength = source.MediaLength,
                MediaItems = source.MediaItems
                    .OrderBy(item => item.SortOrder)
                    .Select(item => new ChatMessageMedia
                    {
                        StorageKey = item.StorageKey,
                        ContentType = item.ContentType,
                        OriginalFileName = item.OriginalFileName,
                        Length = item.Length,
                        SortOrder = item.SortOrder
                    })
                    .ToList(),
                GameSource = source.GameSource,
                GameId = source.GameId,
                GameName = source.GameName,
                GameCoverUrl = source.GameCoverUrl,
                SentAtUtc = now
            };

            target.UpdatedAtUtc = now;
            foreach (var state in target.UserStates.Where(state => state.IsArchived))
            {
                state.IsArchived = false;
                state.ArchivedAtUtc = null;
            }

            _context.ChatMessages.Add(forwardedMessage);
            created.Add((target, recipientId, forwardedMessage));
        }

        await _context.SaveChangesAsync(cancellationToken);

        var streakUpdates = new Dictionary<long, ChatStreakUpdatedDTO?>();
        foreach (var entry in created)
        {
            streakUpdates[entry.Conversation.Id] = await UpdateStreakAfterMessageAsync(
                entry.Conversation,
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var createdIds = created.Select(entry => entry.Message.Id).ToList();
        var loadedMessages = await MessageGraphQuery(userId)
            .Where(message => createdIds.Contains(message.Id))
            .ToDictionaryAsync(message => message.Id, cancellationToken);

        _badgeQueue.Enqueue(userId);
        return new ChatForwardMessagesResultDTO
        {
            Items = created
                .OrderBy(entry => targetIds.IndexOf(entry.Conversation.Id))
                .Select(entry => new ChatForwardedMessageDTO
                {
                    ConversationId = entry.Conversation.Id,
                    RecipientUserId = entry.RecipientId,
                    Message = MapMessage(loadedMessages[entry.Message.Id], userId),
                    StreakUpdate = streakUpdates[entry.Conversation.Id]
                })
                .ToList()
        };
    }

    public Task<ChatRequestDecisionDTO> AcceptRequestAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default) =>
        DecideRequestAsync(
            userId,
            conversationId,
            ChatRequestStatus.Accepted,
            cancellationToken);

    public Task<ChatRequestDecisionDTO> RejectRequestAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default) =>
        DecideRequestAsync(
            userId,
            conversationId,
            ChatRequestStatus.Rejected,
            cancellationToken);

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
                x.ReadAtUtc == null &&
                x.DeletedForEveryoneAtUtc == null &&
                !x.UserStates.Any(state =>
                    state.UserId == userId && state.IsDeleted))
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
                x.DeletedForEveryoneAtUtc == null &&
                !x.UserStates.Any(state =>
                    state.UserId == userId && state.IsDeleted) &&
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

    public async Task<IReadOnlyList<ChatMessageDTO>> GetPinnedMessagesAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureParticipantAsync(userId, conversationId, cancellationToken);

        var rows = await MessageGraphQuery(userId)
            .Where(x =>
                x.ConversationId == conversationId &&
                x.DeletedForEveryoneAtUtc == null &&
                !x.UserStates.Any(state =>
                    state.UserId == userId && state.IsDeleted) &&
                x.UserStates.Any(state =>
                    state.UserId == userId && state.PinnedAtUtc != null))
            .OrderByDescending(x => x.UserStates
                .Where(state => state.UserId == userId)
                .Select(state => state.PinnedAtUtc)
                .FirstOrDefault())
            .Take(100)
            .ToListAsync(cancellationToken);

        return rows.Select(message => MapMessage(message, userId)).ToList();
    }

    public async Task<ChatMessageDeletedDTO> DeleteMessageAsync(
        Guid userId,
        long messageId,
        ChatMessageDeleteScope scope,
        CancellationToken cancellationToken = default)
    {
        if (scope != ChatMessageDeleteScope.ForMe &&
            scope != ChatMessageDeleteScope.ForEveryone)
        {
            throw new BadRequestException("Mesaj silmə növü düzgün deyil.");
        }

        var message = await _context.ChatMessages
            .Include(x => x.Conversation)
            .Include(x => x.UserStates)
            .Include(x => x.Reactions)
            .Include(x => x.MediaItems)
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken)
            ?? throw new NotFoundException("Mesaj tapılmadı.");

        if (message.Conversation.UserOneId != userId &&
            message.Conversation.UserTwoId != userId)
        {
            throw new ForbiddenException("Bu mesaja giriş yoxdur.");
        }

        var now = DateTime.UtcNow;
        var mediaKeysToDelete = message.MediaItems
            .Select(item => item.StorageKey)
            .Append(message.MediaStorageKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (scope == ChatMessageDeleteScope.ForEveryone)
        {
            if (message.SenderUserId != userId)
                throw new ForbiddenException("Mesajı hər kəsdən yalnız göndərən silə bilər.");

            if (!message.DeletedForEveryoneAtUtc.HasValue)
            {
                _context.ChatMessageMedia.RemoveRange(message.MediaItems);
                message.Kind = ChatMessageKind.Text;
                message.Text = null;
                message.MediaStorageKey = null;
                message.MediaContentType = null;
                message.MediaLength = null;
                message.GameSource = null;
                message.GameId = null;
                message.GameName = null;
                message.GameCoverUrl = null;
                message.DeletedForEveryoneAtUtc = now;

                foreach (var state in message.UserStates)
                    state.PinnedAtUtc = null;
                _context.ChatMessageReactions.RemoveRange(message.Reactions);

                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            var otherParticipantId = message.Conversation.UserOneId == userId
                ? message.Conversation.UserTwoId
                : message.Conversation.UserOneId;
            var otherParticipantAlreadyDeleted = message.UserStates.Any(state =>
                state.UserId == otherParticipantId && state.IsDeleted);

            if (otherParticipantAlreadyDeleted)
            {
                var isReplyTarget = await _context.ChatMessages
                    .AsNoTracking()
                    .AnyAsync(x => x.ReplyToMessageId == message.Id, cancellationToken);

                if (isReplyTarget)
                {
                    _context.ChatMessageMedia.RemoveRange(message.MediaItems);
                    message.Kind = ChatMessageKind.Text;
                    message.Text = null;
                    message.MediaStorageKey = null;
                    message.MediaContentType = null;
                    message.MediaLength = null;
                    message.GameSource = null;
                    message.GameId = null;
                    message.GameName = null;
                    message.GameCoverUrl = null;
                    message.DeletedForEveryoneAtUtc = now;
                    foreach (var state in message.UserStates)
                        state.PinnedAtUtc = null;
                    _context.ChatMessageReactions.RemoveRange(message.Reactions);
                }
                else
                {
                    _context.ChatMessages.Remove(message);
                }
            }
            else
            {
                var state = message.UserStates.FirstOrDefault(x => x.UserId == userId);
                if (state is null)
                {
                    state = new ChatMessageUserState
                    {
                        MessageId = message.Id,
                        UserId = userId,
                        IsDeleted = true,
                        DeletedAtUtc = now
                    };
                    _context.ChatMessageUserStates.Add(state);
                }
                else
                {
                    state.IsDeleted = true;
                    state.DeletedAtUtc ??= now;
                    state.PinnedAtUtc = null;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        foreach (var mediaKey in mediaKeysToDelete)
        {
            var stillReferencedByMessage = await _context.ChatMessages
                .AsNoTracking()
                .AnyAsync(item => item.MediaStorageKey == mediaKey, cancellationToken);
            var stillReferencedByCollection = await _context.ChatMessageMedia
                .AsNoTracking()
                .AnyAsync(item => item.StorageKey == mediaKey, cancellationToken);

            if (!stillReferencedByMessage && !stillReferencedByCollection)
                await _fileStorage.DeleteChatMediaAsync(mediaKey);
        }

        return new ChatMessageDeletedDTO
        {
            MessageId = messageId,
            ConversationId = message.ConversationId,
            DeletedByUserId = userId,
            Scope = scope,
            DeletedAtUtc = now
        };
    }

    public async Task<ChatMessagePinChangedDTO> SetMessagePinAsync(
        Guid userId,
        long messageId,
        bool isPinned,
        CancellationToken cancellationToken = default)
    {
        var message = await _context.ChatMessages
            .Include(x => x.Conversation)
            .Include(x => x.UserStates)
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken)
            ?? throw new NotFoundException("Mesaj tapılmadı.");

        if (message.Conversation.UserOneId != userId &&
            message.Conversation.UserTwoId != userId)
            throw new ForbiddenException("Bu mesaja giriş yoxdur.");
        if (message.DeletedForEveryoneAtUtc.HasValue)
            throw new ConflictException("Silinmiş mesaj bərkidilə bilməz.");

        var state = message.UserStates.FirstOrDefault(x => x.UserId == userId);
        if (state?.IsDeleted == true)
            throw new NotFoundException("Mesaj tapılmadı.");

        if (state is null && !isPinned)
        {
            return new ChatMessagePinChangedDTO
            {
                MessageId = messageId,
                ConversationId = message.ConversationId,
                UserId = userId,
                IsPinned = false
            };
        }

        if (state is null)
        {
            state = new ChatMessageUserState
            {
                MessageId = message.Id,
                UserId = userId,
                PinnedAtUtc = DateTime.UtcNow
            };
            _context.ChatMessageUserStates.Add(state);
        }
        else if (isPinned)
        {
            state.PinnedAtUtc = DateTime.UtcNow;
        }
        else if (!state.IsDeleted)
        {
            _context.ChatMessageUserStates.Remove(state);
        }
        else
        {
            state.PinnedAtUtc = null;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return new ChatMessagePinChangedDTO
        {
            MessageId = messageId,
            ConversationId = message.ConversationId,
            UserId = userId,
            IsPinned = isPinned,
            PinnedAtUtc = isPinned ? state.PinnedAtUtc : null
        };
    }

    public async Task<ChatMessageReactionChangedDTO> SetMessageReactionAsync(
        Guid userId,
        long messageId,
        ChatReactionType? reactionType,
        CancellationToken cancellationToken = default)
    {
        if (reactionType.HasValue && !Enum.IsDefined(typeof(ChatReactionType), reactionType.Value))
            throw new BadRequestException("Reaksiya növü düzgün deyil.");

        var message = await _context.ChatMessages
            .Include(x => x.Conversation)
            .Include(x => x.Reactions)
            .Include(x => x.UserStates)
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken)
            ?? throw new NotFoundException("Mesaj tapılmadı.");

        if (message.Conversation.UserOneId != userId &&
            message.Conversation.UserTwoId != userId)
            throw new ForbiddenException("Bu mesaja giriş yoxdur.");
        if (message.DeletedForEveryoneAtUtc.HasValue ||
            message.UserStates.Any(x => x.UserId == userId && x.IsDeleted))
            throw new NotFoundException("Mesaj tapılmadı.");

        var existing = message.Reactions.FirstOrDefault(x => x.UserId == userId);
        var now = DateTime.UtcNow;
        if (!reactionType.HasValue)
        {
            if (existing is not null)
                _context.ChatMessageReactions.Remove(existing);
        }
        else if (existing is null)
        {
            message.Reactions.Add(new ChatMessageReaction
            {
                MessageId = message.Id,
                UserId = userId,
                ReactionType = reactionType.Value,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }
        else
        {
            existing.ReactionType = reactionType.Value;
            existing.UpdatedAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        var reactions = await GetReactionSummariesAsync(
            messageId,
            userId,
            cancellationToken);

        return new ChatMessageReactionChangedDTO
        {
            MessageId = messageId,
            ConversationId = message.ConversationId,
            UserId = userId,
            ReactionType = reactionType,
            Reactions = reactions
        };
    }

    public async Task<ChatConversationArchiveChangedDTO> SetConversationArchivedAsync(
        Guid userId,
        long conversationId,
        bool isArchived,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _context.ChatConversations
            .Include(x => x.UserStates)
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken)
            ?? throw new NotFoundException("Söhbət tapılmadı.");

        if (conversation.UserOneId != userId && conversation.UserTwoId != userId)
            throw new ForbiddenException("Bu söhbətə giriş yoxdur.");
        if (conversation.RequestStatus != ChatRequestStatus.Accepted)
            throw new ConflictException("Yalnız aktiv söhbətlər arxivlənə bilər.");

        var state = conversation.UserStates.FirstOrDefault(x => x.UserId == userId);
        if (state is null)
        {
            state = new ChatConversationUserState
            {
                ConversationId = conversationId,
                UserId = userId
            };
            _context.ChatConversationUserStates.Add(state);
        }

        state.IsArchived = isArchived;
        state.ArchivedAtUtc = isArchived ? DateTime.UtcNow : null;
        await _context.SaveChangesAsync(cancellationToken);

        return new ChatConversationArchiveChangedDTO
        {
            ConversationId = conversationId,
            UserId = userId,
            IsArchived = isArchived,
            ArchivedAtUtc = state.ArchivedAtUtc
        };
    }

    public async Task<ChatConversationDeletedDTO> DeleteConversationForUserAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _context.ChatConversations
            .Include(x => x.UserStates)
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken)
            ?? throw new NotFoundException("Söhbət tapılmadı.");

        if (conversation.UserOneId != userId && conversation.UserTwoId != userId)
            throw new ForbiddenException("Bu söhbətə giriş yoxdur.");
        if (conversation.RequestStatus == ChatRequestStatus.Rejected)
            throw new ConflictException("Rədd edilmiş söhbət silinə bilməz.");

        var visibleMessageIds = await _context.ChatMessages
            .AsNoTracking()
            .Where(message =>
                message.ConversationId == conversationId &&
                !message.UserStates.Any(state =>
                    state.UserId == userId && state.IsDeleted))
            .Select(message => message.Id)
            .ToListAsync(cancellationToken);

        var existingStates = await _context.ChatMessageUserStates
            .Where(state =>
                state.UserId == userId &&
                state.Message.ConversationId == conversationId)
            .ToDictionaryAsync(state => state.MessageId, cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var messageId in visibleMessageIds)
        {
            if (!existingStates.TryGetValue(messageId, out var state))
            {
                state = new ChatMessageUserState
                {
                    MessageId = messageId,
                    UserId = userId
                };
                _context.ChatMessageUserStates.Add(state);
            }

            state.IsDeleted = true;
            state.DeletedAtUtc = now;
            state.PinnedAtUtc = null;
        }

        var conversationState = conversation.UserStates
            .FirstOrDefault(state => state.UserId == userId);
        if (conversationState is not null)
        {
            conversationState.IsArchived = false;
            conversationState.ArchivedAtUtc = null;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new ChatConversationDeletedDTO
        {
            ConversationId = conversationId,
            UserId = userId,
            DeletedMessageCount = visibleMessageIds.Count,
            DeletedAtUtc = now
        };
    }

    public async Task<ChatConversationParticipantsDTO> GetParticipantsAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default)
    {
        var participants = await _context.ChatConversations
            .AsNoTracking()
            .Where(x =>
                x.Id == conversationId &&
                (x.UserOneId == userId || x.UserTwoId == userId) &&
                x.RequestStatus != ChatRequestStatus.Rejected)
            .Select(x => new ChatConversationParticipantsDTO
            {
                ConversationId = x.Id,
                UserOneId = x.UserOneId,
                UserTwoId = x.UserTwoId
            })
            .FirstOrDefaultAsync(cancellationToken);

        return participants ?? throw new NotFoundException("Söhbət tapılmadı.");
    }

    public async Task<StoredChatMediaDTO> OpenMediaAsync(
        Guid userId,
        long messageId,
        long? mediaId = null,
        CancellationToken cancellationToken = default)
    {
        var message = await _context.ChatMessages
            .AsNoTracking()
            .Where(x =>
                x.Id == messageId &&
                x.DeletedForEveryoneAtUtc == null &&
                !x.UserStates.Any(state =>
                    state.UserId == userId && state.IsDeleted) &&
                (x.Conversation.UserOneId == userId ||
                 x.Conversation.UserTwoId == userId))
            .Select(x => new
            {
                x.MediaStorageKey,
                Media = x.MediaItems
                    .Where(item => !mediaId.HasValue || item.Id == mediaId.Value)
                    .OrderBy(item => item.SortOrder)
                    .Select(item => item.StorageKey)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Mesaj mediası tapılmadı.");

        var storageKey = mediaId.HasValue
            ? message.Media
            : message.Media ?? message.MediaStorageKey;
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new NotFoundException("Mesaj mediası tapılmadı.");

        return await _fileStorage.OpenChatMediaAsync(storageKey, cancellationToken);
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

    private IQueryable<ChatMessage> MessageGraphQuery(Guid userId) =>
        _context.ChatMessages
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.UserStates)
            .Include(x => x.Reactions)
            .Include(x => x.MediaItems)
            .Include(x => x.ReplyToMessage)
                .ThenInclude(x => x!.SenderUser)
            .Include(x => x.ReplyToMessage)
                .ThenInclude(x => x!.UserStates);

    private async Task<Dictionary<long, ChatMessageDTO>> LoadMessagesByIdsAsync(
        Guid userId,
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return [];

        var messages = await MessageGraphQuery(userId)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        return messages.ToDictionary(
            x => x.Id,
            x => MapMessage(x, userId));
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

    private async Task<ChatStreakUpdatedDTO?> UpdateStreakAfterMessageAsync(
        ChatConversation conversation,
        CancellationToken cancellationToken)
    {
        var today = GetBakuDate(DateTime.UtcNow);
        if (conversation.LastCompletedStreakDate == today)
            return null;

        var (dayStartUtc, dayEndUtc) = GetUtcDayRange(today);
        var participantCount = await _context.ChatMessages
            .AsNoTracking()
            .Where(x =>
                x.ConversationId == conversation.Id &&
                x.SentAtUtc >= dayStartUtc &&
                x.SentAtUtc < dayEndUtc &&
                x.DeletedForEveryoneAtUtc == null)
            .Select(x => x.SenderUserId)
            .Distinct()
            .CountAsync(cancellationToken);

        if (participantCount < 2)
            return null;

        conversation.CurrentStreak = conversation.LastCompletedStreakDate == today.AddDays(-1)
            ? conversation.CurrentStreak + 1
            : 1;
        conversation.LongestStreak = Math.Max(
            conversation.LongestStreak,
            conversation.CurrentStreak);
        conversation.LastCompletedStreakDate = today;

        return new ChatStreakUpdatedDTO
        {
            ConversationId = conversation.Id,
            WasIncremented = true,
            Streak = MapStreak(conversation)
        };
    }

    private async Task<IReadOnlyList<ChatReactionSummaryDTO>> GetReactionSummariesAsync(
        long messageId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var rows = await _context.ChatMessageReactions
            .AsNoTracking()
            .Where(x => x.MessageId == messageId)
            .GroupBy(x => x.ReactionType)
            .Select(group => new ChatReactionSummaryDTO
            {
                ReactionType = group.Key,
                Count = group.Count(),
                ReactedByCurrentUser = group.Any(x => x.UserId == currentUserId)
            })
            .OrderBy(x => x.ReactionType)
            .ToListAsync(cancellationToken);

        return rows;
    }

    private ChatConversationDTO MapConversation(
        ConversationRow row,
        Guid userId,
        ChatMessageDTO? lastMessage) =>
        new()
        {
            Id = row.Id,
            OtherUser = new ChatUserDTO
            {
                Id = row.OtherUserId,
                UserName = row.OtherUserName,
                ProfileImageUrl = row.OtherUserImage,
                IsOnline = _presenceTracker.IsOnline(row.OtherUserId),
                LastSeenAtUtc = row.OtherUserLastSeenAtUtc
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
            IsArchived = row.IsArchived,
            UnreadCount = row.UnreadCount,
            LastMessage = lastMessage,
            Streak = MapStreak(
                row.CurrentStreak,
                row.LongestStreak,
                row.LastCompletedStreakDate),
            UpdatedAtUtc = row.UpdatedAtUtc
        };

    private static ChatMessageDTO MapMessage(ChatMessage message, Guid userId)
    {
        var deleted = message.DeletedForEveryoneAtUtc.HasValue;
        var state = message.UserStates.FirstOrDefault(x => x.UserId == userId);
        var reactions = message.Reactions
            .GroupBy(x => x.ReactionType)
            .OrderBy(group => group.Key)
            .Select(group => new ChatReactionSummaryDTO
            {
                ReactionType = group.Key,
                Count = group.Count(),
                ReactedByCurrentUser = group.Any(x => x.UserId == userId)
            })
            .ToList();

        var mediaItems = deleted
            ? new List<ChatMessageMediaDTO>()
            : message.MediaItems
                .OrderBy(item => item.SortOrder)
                .Select(item => new ChatMessageMediaDTO
                {
                    Id = item.Id,
                    Url = $"/chat/messages/{message.Id}/media/{item.Id}",
                    ContentType = item.ContentType,
                    FileName = item.OriginalFileName,
                    Length = item.Length,
                    SortOrder = item.SortOrder
                })
                .ToList();

        if (!deleted && mediaItems.Count == 0 && message.MediaStorageKey is not null)
        {
            mediaItems.Add(new ChatMessageMediaDTO
            {
                Id = 0,
                Url = $"/chat/messages/{message.Id}/media",
                ContentType = message.MediaContentType ?? "application/octet-stream",
                FileName = "media",
                Length = message.MediaLength ?? 0,
                SortOrder = 0
            });
        }

        ChatReplyPreviewDTO? reply = null;
        if (message.ReplyToMessage is not null)
        {
            var replyDeleted =
                message.ReplyToMessage.DeletedForEveryoneAtUtc.HasValue ||
                message.ReplyToMessage.UserStates.Any(x =>
                    x.UserId == userId && x.DeletedAtUtc.HasValue);
            reply = new ChatReplyPreviewDTO
            {
                MessageId = message.ReplyToMessage.Id,
                SenderUserId = message.ReplyToMessage.SenderUserId,
                SenderUserName = message.ReplyToMessage.SenderUser?.UserName ?? "İstifadəçi",
                Kind = message.ReplyToMessage.Kind,
                Text = replyDeleted ? null : message.ReplyToMessage.Text,
                GameName = replyDeleted ? null : message.ReplyToMessage.GameName,
                IsDeleted = replyDeleted
            };
        }

        return new ChatMessageDTO
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderUserId = message.SenderUserId,
            Kind = message.Kind,
            Text = deleted ? null : message.Text,
            MediaUrl = deleted || message.MediaStorageKey is null
                ? null
                : $"/chat/messages/{message.Id}/media",
            MediaContentType = deleted ? null : message.MediaContentType,
            MediaItems = mediaItems,
            GameSource = deleted ? null : message.GameSource,
            GameId = deleted ? null : message.GameId,
            GameName = deleted ? null : message.GameName,
            GameCoverUrl = deleted ? null : message.GameCoverUrl,
            GameDetailUrl = !deleted &&
                            message.GameSource.HasValue &&
                            message.GameId.HasValue
                ? $"/games/{message.GameSource.Value}/{message.GameId.Value}"
                : null,
            ReplyTo = reply,
            IsForwarded = message.IsForwarded,
            Reactions = deleted ? [] : reactions,
            SentAtUtc = message.SentAtUtc,
            DeliveredAtUtc = message.DeliveredAtUtc,
            ReadAtUtc = message.ReadAtUtc,
            DeliveryStatus = message.ReadAtUtc.HasValue
                ? ChatMessageDeliveryStatus.Read
                : message.DeliveredAtUtc.HasValue
                    ? ChatMessageDeliveryStatus.Delivered
                    : ChatMessageDeliveryStatus.Sent,
            IsDeletedForEveryone = deleted,
            DeletedForEveryoneAtUtc = message.DeletedForEveryoneAtUtc,
            IsPinnedByCurrentUser = state?.PinnedAtUtc.HasValue == true,
            PinnedAtUtc = state?.PinnedAtUtc
        };
    }

    private static ChatStreakDTO MapStreak(ChatConversation conversation) =>
        MapStreak(
            conversation.CurrentStreak,
            conversation.LongestStreak,
            conversation.LastCompletedStreakDate);

    private static ChatStreakDTO MapStreak(
        int currentStreak,
        int longestStreak,
        DateOnly? lastCompletedDate)
    {
        var today = GetBakuDate(DateTime.UtcNow);
        var active = lastCompletedDate.HasValue &&
                     lastCompletedDate.Value >= today.AddDays(-1);
        var visibleCurrent = active ? currentStreak : 0;
        return new ChatStreakDTO
        {
            CurrentCount = visibleCurrent,
            LongestCount = longestStreak,
            LastCompletedDate = lastCompletedDate,
            IsCompletedToday = lastCompletedDate == today,
            IsActive = active && visibleCurrent > 0
        };
    }

    private static (Guid UserOneId, Guid UserTwoId) NormalizePair(
        Guid left,
        Guid right) =>
        left.CompareTo(right) < 0 ? (left, right) : (right, left);

    private static string? NormalizeConversationSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        if (normalized.Length > 100)
            throw new BadRequestException("Söhbət axtarışı maksimum 100 simvol ola bilər.");
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private static string NormalizeMediaFileName(string? value)
    {
        var fileName = Path.GetFileName(value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fileName))
            return "media";
        return fileName.Length <= 255 ? fileName : fileName[..255];
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

    private static string EscapeLikePattern(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);

    private static DateOnly GetBakuDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            BakuTimeZone));

    private static (DateTime StartUtc, DateTime EndUtc) GetUtcDayRange(DateOnly date)
    {
        var localStart = DateTime.SpecifyKind(
            date.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        var localEnd = localStart.AddDays(1);
        return (
            TimeZoneInfo.ConvertTimeToUtc(localStart, BakuTimeZone),
            TimeZoneInfo.ConvertTimeToUtc(localEnd, BakuTimeZone));
    }

    private static TimeZoneInfo ResolveBakuTimeZone()
    {
        foreach (var id in new[] { "Asia/Baku", "Azerbaijan Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the next platform-specific identifier.
            }
            catch (InvalidTimeZoneException)
            {
                // Try the next platform-specific identifier.
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "WellPlayd-Baku",
            TimeSpan.FromHours(4),
            "Baku",
            "Baku");
    }

    private sealed class ConversationRow
    {
        public long Id { get; init; }
        public Guid UserOneId { get; init; }
        public Guid UserTwoId { get; init; }
        public Guid InitiatedByUserId { get; init; }
        public ChatRequestStatus RequestStatus { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
        public int CurrentStreak { get; init; }
        public int LongestStreak { get; init; }
        public DateOnly? LastCompletedStreakDate { get; init; }
        public bool IsArchived { get; init; }
        public Guid OtherUserId { get; init; }
        public string OtherUserName { get; init; } = string.Empty;
        public string? OtherUserImage { get; init; }
        public DateTime? OtherUserLastSeenAtUtc { get; init; }
        public int UnreadCount { get; init; }
        public int MessageCount { get; init; }
        public long? LastMessageId { get; init; }
    }
}
