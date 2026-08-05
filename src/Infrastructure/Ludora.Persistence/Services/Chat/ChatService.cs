using System.Data;
using Ludora.Application.DTOs.Chat;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed partial class ChatService : IChatService
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
                (x.IsGroup
                    ? x.Members.Any(member => member.UserId == userId)
                    : x.UserOneId == userId || x.UserTwoId == userId) &&
                (x.IsGroup ||
                 x.RequestStatus == ChatRequestStatus.Accepted ||
                 (x.RequestStatus == ChatRequestStatus.Pending &&
                  x.InitiatedByUserId == userId)) &&
                (x.IsGroup || x.Messages.Any(message =>
                    !message.UserStates.Any(state =>
                        state.UserId == userId && state.IsDeleted))) &&
                !x.UserStates.Any(state =>
                    state.UserId == userId && state.IsDeleted) &&
                (archived
                    ? x.UserStates.Any(state =>
                        state.UserId == userId && state.IsArchived)
                    : !x.UserStates.Any(state =>
                        state.UserId == userId && state.IsArchived)) &&
                (normalizedSearch == null ||
                 (x.IsGroup
                    ? x.GroupName!.Contains(normalizedSearch)
                    : x.UserOneId == userId
                        ? x.UserTwo!.UserName!.Contains(normalizedSearch)
                        : x.UserOne!.UserName!.Contains(normalizedSearch))))
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
                IsGroup = x.IsGroup,
                GroupName = x.GroupName,
                GroupDescription = x.GroupDescription,
                GroupImageUrl = x.GroupImageUrl,
                IsGroupCreator = x.IsGroup && x.InitiatedByUserId == userId,
                MemberCount = x.IsGroup ? x.Members.Count : 2,
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
                OtherUserId = x.IsGroup
                    ? null
                    : x.UserOneId == userId ? x.UserTwoId : x.UserOneId,
                OtherUserName = x.IsGroup
                    ? null
                    : x.UserOneId == userId
                        ? x.UserTwo!.UserName!
                        : x.UserOne!.UserName!,
                OtherUserImage = x.IsGroup
                    ? null
                    : x.UserOneId == userId
                        ? x.UserTwo!.ProfileImageUrl
                        : x.UserOne!.ProfileImageUrl,
                OtherUserLastSeenAtUtc = x.IsGroup
                    ? null
                    : x.UserOneId == userId
                        ? x.UserTwo!.RefreshSessions
                            .OrderByDescending(session => session.LastActiveAtUtc)
                            .Select(session => (DateTime?)session.LastActiveAtUtc)
                            .FirstOrDefault()
                        : x.UserOne!.RefreshSessions
                            .OrderByDescending(session => session.LastActiveAtUtc)
                            .Select(session => (DateTime?)session.LastActiveAtUtc)
                            .FirstOrDefault(),
                UnreadCount = x.IsGroup
                    ? x.Messages.Count(message =>
                        message.SenderUserId != userId &&
                        message.DeletedForEveryoneAtUtc == null &&
                        x.Members.Any(member =>
                            member.UserId == userId &&
                            message.SentAtUtc >= member.JoinedAtUtc) &&
                        message.UserStates.Any(state =>
                            state.UserId == userId &&
                            !state.IsDeleted &&
                            state.ReadAtUtc == null))
                    : x.Messages.Count(message =>
                        message.SenderUserId != userId &&
                        message.ReadAtUtc == null &&
                        message.DeletedForEveryoneAtUtc == null &&
                        !message.UserStates.Any(state =>
                            state.UserId == userId && state.IsDeleted)),
                MessageCount = x.IsGroup
                    ? x.Messages.Count(message => x.Members.Any(member =>
                        member.UserId == userId &&
                        message.SentAtUtc >= member.JoinedAtUtc))
                    : x.Messages.Count,
                LastMessageId = x.Messages
                    .Where(message =>
                        (!x.IsGroup || x.Members.Any(member =>
                            member.UserId == userId &&
                            message.SentAtUtc >= member.JoinedAtUtc)) &&
                        !message.UserStates.Any(state =>
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
        var onlineMemberCounts = await GetOnlineGroupMemberCountsAsync(
            rows,
            cancellationToken);

        return rows
            .Select(row => MapConversation(
                row,
                userId,
                row.LastMessageId.HasValue &&
                lastMessages.TryGetValue(row.LastMessageId.Value, out var message)
                    ? message
                    : null,
                onlineMemberCounts.GetValueOrDefault(row.Id)))
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
                !x.IsGroup &&
                (x.UserOneId == userId || x.UserTwoId == userId) &&
                x.RequestStatus == ChatRequestStatus.Pending &&
                x.InitiatedByUserId != userId &&
                x.Messages.Any(message =>
                    !message.UserStates.Any(state =>
                        state.UserId == userId && state.IsDeleted)) &&
                (normalizedSearch == null ||
                 (x.UserOneId == userId
                    ? x.UserTwo!.UserName!.Contains(normalizedSearch)
                    : x.UserOne!.UserName!.Contains(normalizedSearch))))
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(100)
            .Select(x => new ConversationRow
            {
                Id = x.Id,
                IsGroup = false,
                MemberCount = 2,
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
                    ? x.UserTwo!.UserName!
                    : x.UserOne!.UserName!,
                OtherUserImage = x.UserOneId == userId
                    ? x.UserTwo!.ProfileImageUrl
                    : x.UserOne!.ProfileImageUrl,
                OtherUserLastSeenAtUtc = x.UserOneId == userId
                    ? x.UserTwo!.RefreshSessions
                        .OrderByDescending(session => session.LastActiveAtUtc)
                        .Select(session => (DateTime?)session.LastActiveAtUtc)
                        .FirstOrDefault()
                    : x.UserOne!.RefreshSessions
                        .OrderByDescending(session => session.LastActiveAtUtc)
                        .Select(session => (DateTime?)session.LastActiveAtUtc)
                        .FirstOrDefault(),
                UnreadCount = x.Messages.Count(message =>
                    message.SenderUserId != userId &&
                    message.ReadAtUtc == null &&
                    message.DeletedForEveryoneAtUtc == null &&
                    !message.UserStates.Any(state =>
                        state.UserId == userId && state.IsDeleted)),
                MessageCount = x.IsGroup
                    ? x.Messages.Count(message => x.Members.Any(member =>
                        member.UserId == userId &&
                        message.SentAtUtc >= member.JoinedAtUtc))
                    : x.Messages.Count,
                LastMessageId = x.Messages
                    .Where(message =>
                        (!x.IsGroup || x.Members.Any(member =>
                            member.UserId == userId &&
                            message.SentAtUtc >= member.JoinedAtUtc)) &&
                        !message.UserStates.Any(state =>
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
        var directPartnerIds = await _context.ChatConversations
            .AsNoTracking()
            .Where(conversation =>
                !conversation.IsGroup &&
                (conversation.UserOneId == userId || conversation.UserTwoId == userId) &&
                conversation.RequestStatus != ChatRequestStatus.Rejected)
            .Select(conversation =>
                (conversation.UserOneId == userId
                    ? conversation.UserTwoId
                    : conversation.UserOneId)!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var groupPartnerIds = await _context.ChatConversationMembers
            .AsNoTracking()
            .Where(member =>
                member.UserId != userId &&
                member.Conversation.IsGroup &&
                member.Conversation.Members.Any(currentMember =>
                    currentMember.UserId == userId))
            .Select(member => member.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return directPartnerIds
            .Concat(groupPartnerIds)
            .Distinct()
            .ToList();
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
                (!x.Conversation.IsGroup || x.Conversation.Members.Any(member =>
                    member.UserId == userId &&
                    x.SentAtUtc >= member.JoinedAtUtc)) &&
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
                    (!x.Conversation.IsGroup || x.Conversation.Members.Any(member =>
                        member.UserId == userId &&
                        x.SentAtUtc >= member.JoinedAtUtc)) &&
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
                (!x.Conversation.IsGroup || x.Conversation.Members.Any(member =>
                    member.UserId == userId &&
                    x.SentAtUtc >= member.JoinedAtUtc)) &&
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

        var isConversationSend = dto.ConversationId.HasValue;
        if (senderUserId == Guid.Empty ||
            (!isConversationSend && (!dto.RecipientUserId.HasValue || dto.RecipientUserId == Guid.Empty)) ||
            (isConversationSend && dto.ConversationId <= 0) ||
            (!isConversationSend && senderUserId == dto.RecipientUserId))
        {
            throw new BadRequestException("Mesaj alıcısı düzgün deyil.");
        }

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

        var savedMedia = new List<(string StorageKey, string ContentType, long Length, string FileName)>();
        try
        {
            foreach (var upload in mediaUploads)
            {
                var stored = await _fileStorage.SaveChatMediaAsync(upload, cancellationToken);
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

            ChatConversation conversation;
            if (isConversationSend)
            {
                conversation = await _context.ChatConversations
                    .Include(x => x.UserStates)
                    .Include(x => x.Members)
                    .FirstOrDefaultAsync(x => x.Id == dto.ConversationId!.Value, cancellationToken)
                    ?? throw new NotFoundException("Söhbət tapılmadı.");

                if (!conversation.IsGroup)
                    throw new BadRequestException("Söhbət identifikatoru yalnız qrup mesajı üçün istifadə edilə bilər.");
                if (!conversation.Members.Any(member => member.UserId == senderUserId))
                    throw new ForbiddenException("Bu qrupda mesaj yazmaq icazən yoxdur.");
            }
            else
            {
                var recipientUserId = dto.RecipientUserId!.Value;
                var recipient = await _context.Users
                    .AsNoTracking()
                    .Where(x => x.Id == recipientUserId)
                    .Select(x => new { x.Id, x.IsPrivate })
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new NotFoundException("Mesaj alıcısı tapılmadı.");

                var isBlocked = await _context.UserBlocks
                    .AsNoTracking()
                    .AnyAsync(x =>
                            (x.BlockerId == senderUserId && x.BlockedUserId == recipientUserId) ||
                            (x.BlockerId == recipientUserId && x.BlockedUserId == senderUserId),
                        cancellationToken);
                if (isBlocked)
                    throw new ForbiddenException("Bloklanmış istifadəçi ilə mesajlaşmaq mümkün deyil.");

                var senderIsFollower = await _context.UserFollows
                    .AsNoTracking()
                    .AnyAsync(x =>
                            x.FollowerId == senderUserId &&
                            x.FollowingId == recipientUserId &&
                            x.Status == FollowStatus.Accepted,
                        cancellationToken);

                var (userOneId, userTwoId) = NormalizePair(senderUserId, recipientUserId);
                var existingConversation = await _context.ChatConversations
                    .Include(x => x.UserStates)
                    .FirstOrDefaultAsync(
                        x => !x.IsGroup && x.UserOneId == userOneId && x.UserTwoId == userTwoId,
                        cancellationToken);

                if (existingConversation is null)
                {
                    if (dto.ReplyToMessageId.HasValue)
                        throw new BadRequestException("Yeni söhbətdə cavab mesajı seçilə bilməz.");

                    var requiresRequest = recipient.IsPrivate && !senderIsFollower;
                    conversation = new ChatConversation
                    {
                        IsGroup = false,
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
                    conversation = existingConversation;

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
            }

            if (dto.ReplyToMessageId.HasValue)
            {
                var visibleFromUtc = conversation.IsGroup
                    ? conversation.Members
                        .Where(member => member.UserId == senderUserId)
                        .Select(member => (DateTime?)member.JoinedAtUtc)
                        .FirstOrDefault()
                    : null;
                var replyExists = await _context.ChatMessages
                    .AsNoTracking()
                    .AnyAsync(x =>
                            x.Id == dto.ReplyToMessageId.Value &&
                            x.ConversationId == conversation.Id &&
                            (!visibleFromUtc.HasValue || x.SentAtUtc >= visibleFromUtc.Value) &&
                            x.Kind != ChatMessageKind.System &&
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

            if (conversation.IsGroup)
            {
                foreach (var member in conversation.Members.Where(member => member.UserId != senderUserId))
                {
                    message.UserStates.Add(new ChatMessageUserState
                    {
                        UserId = member.UserId,
                        IsDeleted = false
                    });
                }
            }

            conversation.UpdatedAtUtc = now;
            foreach (var state in conversation.UserStates)
            {
                if (state.IsArchived)
                {
                    state.IsArchived = false;
                    state.ArchivedAtUtc = null;
                }
                if (state.IsDeleted)
                {
                    state.IsDeleted = false;
                    state.DeletedAtUtc = null;
                }
            }

            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync(cancellationToken);

            ChatStreakUpdatedDTO? streakUpdate = null;
            if (!conversation.IsGroup && conversation.RequestStatus == ChatRequestStatus.Accepted)
            {
                streakUpdate = await UpdateStreakAfterMessageAsync(conversation, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var loadedMessage = await MessageGraphQuery(senderUserId)
                .FirstAsync(x => x.Id == message.Id, cancellationToken);

            await _badgeQueue.EnqueueAsync(senderUserId, cancellationToken);
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

        var targetIds = targetConversationIds.Where(id => id > 0).Distinct().ToList();
        if (targetIds.Count == 0)
            throw new BadRequestException("Ən azı bir söhbət seçilməlidir.");
        if (targetIds.Count > 20)
            throw new BadRequestException("Bir mesaj eyni anda maksimum 20 söhbətə yönləndirilə bilər.");

        var source = await _context.ChatMessages
            .AsNoTracking()
            .Include(message => message.Conversation)
                .ThenInclude(conversation => conversation.Members)
            .Include(message => message.UserStates)
            .Include(message => message.MediaItems)
            .FirstOrDefaultAsync(message => message.Id == sourceMessageId, cancellationToken)
            ?? throw new NotFoundException("Yönləndiriləcək mesaj tapılmadı.");

        var sourceParticipant = source.Conversation.IsGroup
            ? source.Conversation.Members.Any(member =>
                member.UserId == userId &&
                source.SentAtUtc >= member.JoinedAtUtc)
            : source.Conversation.UserOneId == userId || source.Conversation.UserTwoId == userId;
        if (!sourceParticipant)
            throw new ForbiddenException("Bu mesaja giriş yoxdur.");
        if (source.Kind == ChatMessageKind.System)
            throw new BadRequestException("Sistem mesajı yönləndirilə bilməz.");
        if (source.DeletedForEveryoneAtUtc.HasValue ||
            source.UserStates.Any(state => state.UserId == userId && state.IsDeleted))
        {
            throw new NotFoundException("Yönləndiriləcək mesaj artıq əlçatan deyil.");
        }
        if (targetIds.Contains(source.ConversationId))
            throw new BadRequestException("Mesaj eyni söhbətə yönləndirilə bilməz.");

        var targets = await _context.ChatConversations
            .Include(conversation => conversation.UserStates)
            .Include(conversation => conversation.Members)
            .Where(conversation => targetIds.Contains(conversation.Id))
            .ToListAsync(cancellationToken);
        if (targets.Count != targetIds.Count)
            throw new NotFoundException("Seçilmiş söhbətlərdən biri tapılmadı.");

        foreach (var target in targets)
        {
            var participant = target.IsGroup
                ? target.Members.Any(member => member.UserId == userId)
                : target.UserOneId == userId || target.UserTwoId == userId;
            if (!participant)
                throw new ForbiddenException("Seçilmiş söhbətlərdən birinə giriş yoxdur.");
            if (!target.IsGroup && target.RequestStatus != ChatRequestStatus.Accepted)
                throw new ConflictException("Mesaj yalnız qəbul edilmiş söhbətlərə yönləndirilə bilər.");
        }

        var directRecipientIds = targets
            .Where(target => !target.IsGroup)
            .Select(target => target.UserOneId == userId ? target.UserTwoId : target.UserOneId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        if (directRecipientIds.Count > 0)
        {
            var hasBlockedConversation = await _context.UserBlocks
                .AsNoTracking()
                .AnyAsync(block =>
                        (directRecipientIds.Contains(block.BlockerId) && block.BlockedUserId == userId) ||
                        (block.BlockerId == userId && directRecipientIds.Contains(block.BlockedUserId)),
                    cancellationToken);
            if (hasBlockedConversation)
                throw new ForbiddenException("Bloklanmış istifadəçi olan söhbətə mesaj yönləndirilə bilməz.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var now = DateTime.UtcNow;
        var created = new List<(ChatConversation Conversation, IReadOnlyList<Guid> RecipientIds, ChatMessage Message)>();

        foreach (var target in targets)
        {
            var recipientIds = target.IsGroup
                ? target.Members.Where(member => member.UserId != userId).Select(member => member.UserId).ToList()
                : new List<Guid> { (target.UserOneId == userId ? target.UserTwoId : target.UserOneId)!.Value };

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

            if (target.IsGroup)
            {
                foreach (var recipientId in recipientIds)
                {
                    forwardedMessage.UserStates.Add(new ChatMessageUserState
                    {
                        UserId = recipientId,
                        IsDeleted = false
                    });
                }
            }

            target.UpdatedAtUtc = now;
            foreach (var state in target.UserStates)
            {
                if (state.IsArchived)
                {
                    state.IsArchived = false;
                    state.ArchivedAtUtc = null;
                }
                if (state.IsDeleted)
                {
                    state.IsDeleted = false;
                    state.DeletedAtUtc = null;
                }
            }

            _context.ChatMessages.Add(forwardedMessage);
            created.Add((target, recipientIds, forwardedMessage));
        }

        await _context.SaveChangesAsync(cancellationToken);
        var streakUpdates = new Dictionary<long, ChatStreakUpdatedDTO?>();
        foreach (var entry in created)
        {
            streakUpdates[entry.Conversation.Id] = entry.Conversation.IsGroup
                ? null
                : await UpdateStreakAfterMessageAsync(entry.Conversation, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var createdIds = created.Select(entry => entry.Message.Id).ToList();
        var loadedMessages = await MessageGraphQuery(userId)
            .Where(message => createdIds.Contains(message.Id))
            .ToDictionaryAsync(message => message.Id, cancellationToken);

        await _badgeQueue.EnqueueAsync(userId, cancellationToken);
        return new ChatForwardMessagesResultDTO
        {
            Items = created
                .OrderBy(entry => targetIds.IndexOf(entry.Conversation.Id))
                .Select(entry => new ChatForwardedMessageDTO
                {
                    ConversationId = entry.Conversation.Id,
                    RecipientUserIds = entry.RecipientIds,
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
        var isGroup = await _context.ChatConversations
            .AsNoTracking()
            .Where(conversation => conversation.Id == conversationId)
            .Select(conversation => conversation.IsGroup)
            .FirstAsync(cancellationToken);

        List<long> messageIds;
        if (isGroup)
        {
            var states = await _context.ChatMessageUserStates
                .Where(state =>
                    state.UserId == userId &&
                    state.Message.ConversationId == conversationId &&
                    state.Message.SenderUserId != userId &&
                    state.Message.Conversation.Members.Any(member =>
                        member.UserId == userId &&
                        state.Message.SentAtUtc >= member.JoinedAtUtc) &&
                    !state.IsDeleted &&
                    state.ReadAtUtc == null &&
                    state.Message.DeletedForEveryoneAtUtc == null)
                .Take(1000)
                .ToListAsync(cancellationToken);

            messageIds = states.Select(state => state.MessageId).ToList();
            foreach (var state in states)
            {
                state.DeliveredAtUtc ??= now;
                state.ReadAtUtc = now;
            }
            if (states.Count > 0)
                await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            messageIds = await _context.ChatMessages
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
        var receipts = new List<ChatReadReceiptDTO>();

        var directRows = await _context.ChatMessages
            .Where(x =>
                !x.Conversation.IsGroup &&
                x.SenderUserId != userId &&
                x.DeliveredAtUtc == null &&
                x.DeletedForEveryoneAtUtc == null &&
                !x.UserStates.Any(state => state.UserId == userId && state.IsDeleted) &&
                (x.Conversation.UserOneId == userId || x.Conversation.UserTwoId == userId))
            .Select(x => new { x.Id, x.ConversationId })
            .Take(1000)
            .ToListAsync(cancellationToken);

        if (directRows.Count > 0)
        {
            var ids = directRows.Select(x => x.Id).ToList();
            await _context.ChatMessages
                .Where(x => ids.Contains(x.Id))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.DeliveredAtUtc, now),
                    cancellationToken);
            receipts.AddRange(directRows
                .GroupBy(x => x.ConversationId)
                .Select(group => new ChatReadReceiptDTO
                {
                    ConversationId = group.Key,
                    ReadByUserId = userId,
                    ReadAtUtc = now,
                    MessageIds = group.Select(x => x.Id).ToList()
                }));
        }

        var groupStates = await _context.ChatMessageUserStates
            .Where(state =>
                state.UserId == userId &&
                state.Message.Conversation.IsGroup &&
                state.Message.SenderUserId != userId &&
                state.Message.Conversation.Members.Any(member =>
                    member.UserId == userId &&
                    state.Message.SentAtUtc >= member.JoinedAtUtc) &&
                !state.IsDeleted &&
                state.DeliveredAtUtc == null &&
                state.Message.DeletedForEveryoneAtUtc == null)
            .Take(1000)
            .ToListAsync(cancellationToken);

        if (groupStates.Count > 0)
        {
            foreach (var state in groupStates)
                state.DeliveredAtUtc = now;
            await _context.SaveChangesAsync(cancellationToken);

            receipts.AddRange(groupStates
                .GroupBy(state => state.Message.ConversationId)
                .Select(group => new ChatReadReceiptDTO
                {
                    ConversationId = group.Key,
                    ReadByUserId = userId,
                    ReadAtUtc = now,
                    MessageIds = group.Select(state => state.MessageId).ToList()
                }));
        }

        return receipts;
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
                (!x.Conversation.IsGroup || x.Conversation.Members.Any(member =>
                    member.UserId == userId &&
                    x.SentAtUtc >= member.JoinedAtUtc)) &&
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
                .ThenInclude(x => x.Members)
            .Include(x => x.UserStates)
            .Include(x => x.Reactions)
            .Include(x => x.MediaItems)
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken)
            ?? throw new NotFoundException("Mesaj tapılmadı.");

        var isParticipant = message.Conversation.IsGroup
            ? message.Conversation.Members.Any(member =>
                member.UserId == userId &&
                message.SentAtUtc >= member.JoinedAtUtc)
            : message.Conversation.UserOneId == userId || message.Conversation.UserTwoId == userId;
        if (!isParticipant)
            throw new ForbiddenException("Bu mesaja giriş yoxdur.");
        if (message.Kind == ChatMessageKind.System)
            throw new BadRequestException("Sistem mesajı silinə bilməz.");

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
                ClearMessageContent(message, now);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            var otherParticipantIds = message.Conversation.IsGroup
                ? message.Conversation.Members
                    .Where(member => member.UserId != userId)
                    .Select(member => member.UserId)
                    .ToList()
                : new List<Guid>
                {
                    (message.Conversation.UserOneId == userId
                        ? message.Conversation.UserTwoId
                        : message.Conversation.UserOneId)!.Value
                };

            var allOtherParticipantsDeleted = otherParticipantIds.All(participantId =>
                message.UserStates.Any(state =>
                    state.UserId == participantId && state.IsDeleted));

            if (allOtherParticipantsDeleted)
            {
                var isReplyTarget = await _context.ChatMessages
                    .AsNoTracking()
                    .AnyAsync(x => x.ReplyToMessageId == message.Id, cancellationToken);

                if (isReplyTarget)
                    ClearMessageContent(message, now);
                else
                    _context.ChatMessages.Remove(message);
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

    private void ClearMessageContent(ChatMessage message, DateTime deletedAtUtc)
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
        message.DeletedForEveryoneAtUtc = deletedAtUtc;
        foreach (var state in message.UserStates)
            state.PinnedAtUtc = null;
        _context.ChatMessageReactions.RemoveRange(message.Reactions);
    }

    public async Task<ChatMessagePinChangedDTO> SetMessagePinAsync(
        Guid userId,
        long messageId,
        bool isPinned,
        CancellationToken cancellationToken = default)
    {
        var message = await _context.ChatMessages
            .Include(x => x.Conversation)
                .ThenInclude(x => x.Members)
            .Include(x => x.UserStates)
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken)
            ?? throw new NotFoundException("Mesaj tapılmadı.");

        var isParticipant = message.Conversation.IsGroup
            ? message.Conversation.Members.Any(member =>
                member.UserId == userId &&
                message.SentAtUtc >= member.JoinedAtUtc)
            : message.Conversation.UserOneId == userId || message.Conversation.UserTwoId == userId;
        if (!isParticipant)
            throw new ForbiddenException("Bu mesaja giriş yoxdur.");
        if (message.Kind == ChatMessageKind.System)
            throw new BadRequestException("Sistem mesajı bərkidilə bilməz.");
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
                .ThenInclude(x => x.Members)
            .Include(x => x.Reactions)
            .Include(x => x.UserStates)
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken)
            ?? throw new NotFoundException("Mesaj tapılmadı.");

        var isParticipant = message.Conversation.IsGroup
            ? message.Conversation.Members.Any(member =>
                member.UserId == userId &&
                message.SentAtUtc >= member.JoinedAtUtc)
            : message.Conversation.UserOneId == userId || message.Conversation.UserTwoId == userId;
        if (!isParticipant)
            throw new ForbiddenException("Bu mesaja giriş yoxdur.");
        if (message.Kind == ChatMessageKind.System)
            throw new BadRequestException("Sistem mesajına reaksiya vermək olmaz.");
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
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken)
            ?? throw new NotFoundException("Söhbət tapılmadı.");

        var isParticipant = conversation.IsGroup
            ? conversation.Members.Any(member => member.UserId == userId)
            : conversation.UserOneId == userId || conversation.UserTwoId == userId;
        if (!isParticipant)
            throw new ForbiddenException("Bu söhbətə giriş yoxdur.");
        if (!conversation.IsGroup && conversation.RequestStatus != ChatRequestStatus.Accepted)
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
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken)
            ?? throw new NotFoundException("Söhbət tapılmadı.");

        var isParticipant = conversation.IsGroup
            ? conversation.Members.Any(member => member.UserId == userId)
            : conversation.UserOneId == userId || conversation.UserTwoId == userId;
        if (!isParticipant)
            throw new ForbiddenException("Bu söhbətə giriş yoxdur.");
        if (!conversation.IsGroup && conversation.RequestStatus == ChatRequestStatus.Rejected)
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
        if (conversationState is null)
        {
            conversationState = new ChatConversationUserState
            {
                ConversationId = conversationId,
                UserId = userId
            };
            _context.ChatConversationUserStates.Add(conversationState);
        }

        conversationState.IsArchived = false;
        conversationState.ArchivedAtUtc = null;
        conversationState.IsDeleted = true;
        conversationState.DeletedAtUtc = now;

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
        var conversation = await _context.ChatConversations
            .AsNoTracking()
            .Where(x =>
                x.Id == conversationId &&
                (x.IsGroup
                    ? x.Members.Any(member => member.UserId == userId)
                    : x.UserOneId == userId || x.UserTwoId == userId) &&
                (x.IsGroup || x.RequestStatus != ChatRequestStatus.Rejected))
            .Select(x => new
            {
                x.Id,
                x.IsGroup,
                x.UserOneId,
                x.UserTwoId,
                MemberIds = x.Members.Select(member => member.UserId).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Söhbət tapılmadı.");

        var userIds = conversation.IsGroup
            ? conversation.MemberIds
            : new[] { conversation.UserOneId!.Value, conversation.UserTwoId!.Value }.ToList();

        return new ChatConversationParticipantsDTO
        {
            ConversationId = conversation.Id,
            UserOneId = conversation.UserOneId,
            UserTwoId = conversation.UserTwoId,
            UserIds = userIds
        };
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
                (x.Conversation.IsGroup
                    ? x.Conversation.Members.Any(member =>
                        member.UserId == userId &&
                        x.SentAtUtc >= member.JoinedAtUtc)
                    : x.Conversation.UserOneId == userId || x.Conversation.UserTwoId == userId))
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

        if (conversation.IsGroup ||
            (conversation.UserOneId != userId && conversation.UserTwoId != userId))
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
            .Include(x => x.SenderUser)
            .Include(x => x.Conversation)
                .ThenInclude(x => x.Members)
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
            .Where(x =>
                ids.Contains(x.Id) &&
                (!x.Conversation.IsGroup || x.Conversation.Members.Any(member =>
                    member.UserId == userId &&
                    x.SentAtUtc >= member.JoinedAtUtc)))
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
                     (x.IsGroup
                         ? x.Members.Any(member => member.UserId == userId)
                         : x.UserOneId == userId || x.UserTwoId == userId) &&
                     (x.IsGroup || x.RequestStatus != ChatRequestStatus.Rejected),
                cancellationToken);

        if (!exists)
            throw new NotFoundException("Söhbət tapılmadı.");
    }

    private async Task<ChatStreakUpdatedDTO?> UpdateStreakAfterMessageAsync(
        ChatConversation conversation,
        CancellationToken cancellationToken)
    {
        if (conversation.IsGroup)
            return null;

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

    private async Task<IReadOnlyDictionary<long, int>> GetOnlineGroupMemberCountsAsync(
        IReadOnlyCollection<ConversationRow> rows,
        CancellationToken cancellationToken)
    {
        var groupIds = rows
            .Where(row => row.IsGroup)
            .Select(row => row.Id)
            .Distinct()
            .ToArray();

        if (groupIds.Length == 0)
            return new Dictionary<long, int>();

        var members = await _context.ChatConversationMembers
            .AsNoTracking()
            .Where(member => groupIds.Contains(member.ConversationId))
            .Select(member => new
            {
                member.ConversationId,
                member.UserId
            })
            .ToListAsync(cancellationToken);

        return members
            .GroupBy(member => member.ConversationId)
            .ToDictionary(
                group => group.Key,
                group => group.Count(member => _presenceTracker.IsOnline(member.UserId)));
    }

    private ChatConversationDTO MapConversation(
        ConversationRow row,
        Guid userId,
        ChatMessageDTO? lastMessage,
        int onlineMemberCount = 0) =>
        new()
        {
            Id = row.Id,
            IsGroup = row.IsGroup,
            GroupName = row.GroupName,
            GroupDescription = row.GroupDescription,
            GroupImageUrl = row.GroupImageUrl,
            IsGroupCreator = row.IsGroupCreator,
            MemberCount = row.MemberCount,
            OnlineMemberCount = row.IsGroup ? onlineMemberCount : 0,
            OtherUser = row.IsGroup || !row.OtherUserId.HasValue
                ? null
                : new ChatUserDTO
                {
                    Id = row.OtherUserId.Value,
                    UserName = row.OtherUserName ?? "İstifadəçi",
                    ProfileImageUrl = row.OtherUserImage,
                    IsOnline = _presenceTracker.IsOnline(row.OtherUserId.Value),
                    LastSeenAtUtc = row.OtherUserLastSeenAtUtc
                },
            RequestStatus = row.RequestStatus,
            IsIncomingRequest =
                !row.IsGroup &&
                row.RequestStatus == ChatRequestStatus.Pending &&
                row.InitiatedByUserId != userId,
            CanSendMessage =
                row.IsGroup ||
                row.RequestStatus == ChatRequestStatus.Accepted ||
                (row.RequestStatus == ChatRequestStatus.Pending &&
                 row.InitiatedByUserId == userId &&
                 row.MessageCount == 0),
            IsArchived = row.IsArchived,
            UnreadCount = row.UnreadCount,
            LastMessage = lastMessage,
            Streak = row.IsGroup
                ? new ChatStreakDTO()
                : MapStreak(
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
            var currentMembership = message.Conversation.IsGroup
                ? message.Conversation.Members.FirstOrDefault(member => member.UserId == userId)
                : null;
            var replyPredatesMembership =
                currentMembership is not null &&
                message.ReplyToMessage.SentAtUtc < currentMembership.JoinedAtUtc;
            var replyDeleted =
                replyPredatesMembership ||
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

        DateTime? deliveredAtUtc;
        DateTime? readAtUtc;
        if (message.Conversation.IsGroup)
        {
            if (message.SenderUserId == userId)
            {
                var recipientStates = message.UserStates
                    .Where(item => item.UserId != userId && !item.IsDeleted)
                    .ToList();
                deliveredAtUtc = recipientStates.Count > 0 && recipientStates.All(item => item.DeliveredAtUtc.HasValue)
                    ? recipientStates.Max(item => item.DeliveredAtUtc)
                    : null;
                readAtUtc = recipientStates.Count > 0 && recipientStates.All(item => item.ReadAtUtc.HasValue)
                    ? recipientStates.Max(item => item.ReadAtUtc)
                    : null;
            }
            else
            {
                deliveredAtUtc = state?.DeliveredAtUtc;
                readAtUtc = state?.ReadAtUtc;
            }
        }
        else
        {
            deliveredAtUtc = message.DeliveredAtUtc;
            readAtUtc = message.ReadAtUtc;
        }

        return new ChatMessageDTO
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderUserId = message.SenderUserId,
            SenderUser = new ChatUserDTO
            {
                Id = message.SenderUserId,
                UserName = message.SenderUser?.UserName ?? "İstifadəçi",
                ProfileImageUrl = message.SenderUser?.ProfileImageUrl,
                IsOnline = false
            },
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
            DeliveredAtUtc = deliveredAtUtc,
            ReadAtUtc = readAtUtc,
            DeliveryStatus = readAtUtc.HasValue
                ? ChatMessageDeliveryStatus.Read
                : deliveredAtUtc.HasValue
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
        public bool IsGroup { get; init; }
        public string? GroupName { get; init; }
        public string? GroupDescription { get; init; }
        public string? GroupImageUrl { get; init; }
        public bool IsGroupCreator { get; init; }
        public int MemberCount { get; init; }
        public Guid? UserOneId { get; init; }
        public Guid? UserTwoId { get; init; }
        public Guid InitiatedByUserId { get; init; }
        public ChatRequestStatus RequestStatus { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
        public int CurrentStreak { get; init; }
        public int LongestStreak { get; init; }
        public DateOnly? LastCompletedStreakDate { get; init; }
        public bool IsArchived { get; init; }
        public Guid? OtherUserId { get; init; }
        public string? OtherUserName { get; init; }
        public string? OtherUserImage { get; init; }
        public DateTime? OtherUserLastSeenAtUtc { get; init; }
        public int UnreadCount { get; init; }
        public int MessageCount { get; init; }
        public long? LastMessageId { get; init; }
    }
}
