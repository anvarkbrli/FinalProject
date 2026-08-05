using System.Security.Cryptography;
using Ludora.Application.DTOs.Chat;
using Ludora.Application.Exceptions;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed partial class ChatService
{
    public async Task<ChatGroupDTO> CreateGroupAsync(
        Guid creatorUserId,
        ChatGroupCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var name = NormalizeGroupName(dto.Name);
        var description = NormalizeGroupDescription(dto.Description);
        var memberIds = dto.MemberUserIds
            .Where(id => id != Guid.Empty && id != creatorUserId)
            .Distinct()
            .ToList();

        if (memberIds.Count == 0)
            throw new BadRequestException("Qrup yaratmaq üçün ən azı bir istifadəçi seçilməlidir.");

        await ValidateGroupMemberCandidatesAsync(
            memberIds,
            memberIds.Prepend(creatorUserId).ToList(),
            cancellationToken);

        string? imageUrl = null;
        try
        {
            if (dto.Image is not null)
                imageUrl = await _fileStorage.SaveChatGroupImageAsync(dto.Image, cancellationToken);

            await using var transaction = await _context.Database
                .BeginTransactionAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var conversation = new ChatConversation
            {
                IsGroup = true,
                GroupName = name,
                GroupDescription = description,
                GroupImageUrl = imageUrl,
                InitiatedByUserId = creatorUserId,
                RequestStatus = ChatRequestStatus.Accepted,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Members = memberIds
                    .Prepend(creatorUserId)
                    .Select(userId => new ChatConversationMember
                    {
                        UserId = userId,
                        AddedByUserId = creatorUserId,
                        JoinedAtUtc = now
                    })
                    .ToList()
            };

            _context.ChatConversations.Add(conversation);
            await AddMemberAddedSystemMessagesAsync(
                conversation,
                creatorUserId,
                memberIds,
                now,
                cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            var result = await GetGroupAsync(
                creatorUserId,
                conversation.Id,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            if (imageUrl is not null)
            {
                try
                {
                    await _fileStorage.DeleteAsync(imageUrl);
                }
                catch
                {
                    // Database consistency is more important than best-effort file cleanup.
                }
            }
            throw;
        }
    }

    public async Task<ChatGroupDTO> GetGroupAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default)
    {
        var group = await _context.ChatConversations
            .AsNoTracking()
            .Where(conversation =>
                conversation.Id == conversationId &&
                conversation.IsGroup &&
                conversation.Members.Any(member => member.UserId == userId))
            .Select(conversation => new ChatGroupDTO
            {
                ConversationId = conversation.Id,
                Name = conversation.GroupName!,
                Description = conversation.GroupDescription,
                ImageUrl = conversation.GroupImageUrl,
                CreatorUserId = conversation.InitiatedByUserId,
                IsCurrentUserCreator = conversation.InitiatedByUserId == userId,
                CreatedAtUtc = conversation.CreatedAtUtc,
                UpdatedAtUtc = conversation.UpdatedAtUtc,
                Members = conversation.Members
                    .OrderBy(member => member.UserId == conversation.InitiatedByUserId ? 0 : 1)
                    .ThenBy(member => member.User.UserName)
                    .Select(member => new ChatGroupMemberDTO
                    {
                        Id = member.UserId,
                        UserName = member.User.UserName!,
                        ProfileImageUrl = member.User.ProfileImageUrl,
                        IsCreator = member.UserId == conversation.InitiatedByUserId,
                        JoinedAtUtc = member.JoinedAtUtc
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return group ?? throw new NotFoundException("Qrup tapılmadı.");
    }

    public async Task<ChatGroupDTO> UpdateGroupAsync(
        Guid userId,
        long conversationId,
        ChatGroupUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var group = await GetGroupForCreatorAsync(userId, conversationId, cancellationToken);
        var name = NormalizeGroupName(dto.Name);
        var description = NormalizeGroupDescription(dto.Description);
        await _context.Entry(group)
            .Collection(conversation => conversation.Members)
            .LoadAsync(cancellationToken);
        var existingMemberIds = group.Members
            .Select(member => member.UserId)
            .ToHashSet();
        var memberIdsToAdd = dto.MemberUserIdsToAdd
            .Where(id => id != Guid.Empty && !existingMemberIds.Contains(id))
            .Distinct()
            .ToList();
        if (memberIdsToAdd.Count > 0)
        {
            await ValidateGroupMemberCandidatesAsync(
                memberIdsToAdd,
                existingMemberIds.Concat(memberIdsToAdd).ToList(),
                cancellationToken);
        }

        var oldImageUrl = group.GroupImageUrl;
        string? newImageUrl = null;

        ChatGroupDTO result;
        try
        {
            if (dto.Image is not null)
                newImageUrl = await _fileStorage.SaveChatGroupImageAsync(dto.Image, cancellationToken);

            await using var transaction = await _context.Database
                .BeginTransactionAsync(cancellationToken);
            group.GroupName = name;
            group.GroupDescription = description;
            if (newImageUrl is not null)
                group.GroupImageUrl = newImageUrl;
            else if (dto.RemoveImage)
                group.GroupImageUrl = null;
            var now = DateTime.UtcNow;
            foreach (var memberId in memberIdsToAdd)
            {
                group.Members.Add(new ChatConversationMember
                {
                    ConversationId = group.Id,
                    UserId = memberId,
                    AddedByUserId = userId,
                    JoinedAtUtc = now
                });
            }
            if (memberIdsToAdd.Count > 0)
            {
                await AddMemberAddedSystemMessagesAsync(
                    group,
                    userId,
                    memberIdsToAdd,
                    now,
                    cancellationToken);
            }
            else
            {
                group.UpdatedAtUtc = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            result = await GetGroupAsync(userId, conversationId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (newImageUrl is not null)
            {
                try
                {
                    await _fileStorage.DeleteAsync(newImageUrl);
                }
                catch
                {
                    // The database transaction is rolled back; cleanup remains best effort.
                }
            }
            throw;
        }

        if ((newImageUrl is not null || dto.RemoveImage) && oldImageUrl is not null)
        {
            try
            {
                await _fileStorage.DeleteAsync(oldImageUrl);
            }
            catch
            {
                // A stale image is preferable to rolling back an already committed update.
            }
        }

        return result;
    }

    public async Task<ChatGroupChangedDTO> AddGroupMembersAsync(
        Guid userId,
        long conversationId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        var group = await GetGroupForCreatorAsync(userId, conversationId, cancellationToken);
        await _context.Entry(group).Collection(conversation => conversation.Members).LoadAsync(cancellationToken);

        var existingMemberIds = group.Members.Select(member => member.UserId).ToHashSet();
        var ids = userIds
            .Where(id => id != Guid.Empty && !existingMemberIds.Contains(id))
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            throw new BadRequestException("Əlavə ediləcək yeni istifadəçi seçilməyib.");

        await ValidateGroupMemberCandidatesAsync(
            ids,
            existingMemberIds.Concat(ids).ToList(),
            cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var memberId in ids)
        {
            group.Members.Add(new ChatConversationMember
            {
                ConversationId = group.Id,
                UserId = memberId,
                AddedByUserId = userId,
                JoinedAtUtc = now
            });
        }
        await AddMemberAddedSystemMessagesAsync(
            group,
            userId,
            ids,
            now,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var participantIds = group.Members.Select(member => member.UserId).Distinct().ToList();
        return new ChatGroupChangedDTO
        {
            ConversationId = group.Id,
            ChangeType = "MembersAdded",
            ParticipantUserIds = participantIds,
            Group = await GetGroupAsync(userId, group.Id, cancellationToken)
        };
    }

    public async Task<ChatGroupChangedDTO> RemoveGroupMemberAsync(
        Guid userId,
        long conversationId,
        Guid memberUserId,
        CancellationToken cancellationToken = default)
    {
        var group = await GetGroupForCreatorAsync(userId, conversationId, cancellationToken);
        if (memberUserId == group.InitiatedByUserId)
            throw new BadRequestException("Qrupu yaradan istifadəçi qrupdan çıxarıla bilməz.");

        await _context.Entry(group).Collection(conversation => conversation.Members).LoadAsync(cancellationToken);
        var member = group.Members.FirstOrDefault(item => item.UserId == memberUserId)
            ?? throw new NotFoundException("İstifadəçi bu qrupda deyil.");

        var broadcastIds = group.Members.Select(item => item.UserId).Distinct().ToList();

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        // Remove private per-user chat state together with membership. Messages and
        // reactions remain part of the shared conversation history, but archived,
        // pinned, read and locally-deleted state must not survive a removal/re-add.
        await _context.ChatMessageUserStates
            .Where(state =>
                state.UserId == memberUserId &&
                state.Message.ConversationId == group.Id)
            .ExecuteDeleteAsync(cancellationToken);
        await _context.ChatConversationUserStates
            .Where(state =>
                state.UserId == memberUserId &&
                state.ConversationId == group.Id)
            .ExecuteDeleteAsync(cancellationToken);

        _context.ChatConversationMembers.Remove(member);
        group.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ChatGroupChangedDTO
        {
            ConversationId = group.Id,
            ChangeType = "MemberRemoved",
            ParticipantUserIds = broadcastIds,
            AffectedUserId = memberUserId,
            Group = await GetGroupAsync(userId, group.Id, cancellationToken)
        };
    }

    public async Task<ChatGroupChangedDTO> LeaveGroupAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken = default)
    {
        var group = await _context.ChatConversations
            .Include(conversation => conversation.Members)
            .FirstOrDefaultAsync(conversation =>
                    conversation.Id == conversationId && conversation.IsGroup,
                cancellationToken)
            ?? throw new NotFoundException("Qrup tapılmadı.");

        var leavingMember = group.Members.FirstOrDefault(member => member.UserId == userId)
            ?? throw new NotFoundException("Bu qrupun üzvü deyilsən.");
        var broadcastIds = group.Members
            .Select(member => member.UserId)
            .Distinct()
            .ToList();
        var remainingMembers = group.Members
            .Where(member => member.UserId != userId)
            .ToList();
        var oldImageUrl = group.GroupImageUrl;
        Guid? newCreatorUserId = null;
        var groupDeleted = remainingMembers.Count == 0;

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        await _context.ChatMessageUserStates
            .Where(state =>
                state.UserId == userId &&
                state.Message.ConversationId == group.Id)
            .ExecuteDeleteAsync(cancellationToken);
        await _context.ChatConversationUserStates
            .Where(state =>
                state.UserId == userId &&
                state.ConversationId == group.Id)
            .ExecuteDeleteAsync(cancellationToken);

        _context.ChatConversationMembers.Remove(leavingMember);

        if (groupDeleted)
        {
            _context.ChatConversations.Remove(group);
        }
        else
        {
            if (group.InitiatedByUserId == userId)
            {
                var selectedIndex = RandomNumberGenerator.GetInt32(remainingMembers.Count);
                newCreatorUserId = remainingMembers[selectedIndex].UserId;
                group.InitiatedByUserId = newCreatorUserId.Value;
            }

            group.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (groupDeleted && !string.IsNullOrWhiteSpace(oldImageUrl))
        {
            try
            {
                await _fileStorage.DeleteAsync(oldImageUrl);
            }
            catch
            {
                // The group has already been removed; file cleanup remains best effort.
            }
        }

        ChatGroupDTO? updatedGroup = null;
        if (!groupDeleted)
        {
            var groupReaderId = newCreatorUserId ?? remainingMembers[0].UserId;
            updatedGroup = await GetGroupAsync(
                groupReaderId,
                conversationId,
                cancellationToken);
        }

        return new ChatGroupChangedDTO
        {
            ConversationId = conversationId,
            ChangeType = groupDeleted ? "GroupDeleted" : "MemberLeft",
            ParticipantUserIds = broadcastIds,
            AffectedUserId = userId,
            NewCreatorUserId = newCreatorUserId,
            GroupDeleted = groupDeleted,
            Group = updatedGroup
        };
    }

    public async Task<ChatBulkDeleteResultDTO> DeleteMessagesAsync(
        Guid userId,
        IReadOnlyCollection<long> messageIds,
        ChatMessageDeleteScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messageIds);
        if (scope != ChatMessageDeleteScope.ForMe &&
            scope != ChatMessageDeleteScope.ForEveryone)
        {
            throw new BadRequestException("Mesaj silmə növü düzgün deyil.");
        }

        var ids = messageIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            throw new BadRequestException("Silinəcək mesaj seçilməyib.");
        if (ids.Count > 500)
            throw new BadRequestException("Bir əməliyyatda maksimum 500 mesaj silinə bilər.");

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        var messages = await _context.ChatMessages
            .Include(message => message.Conversation)
                .ThenInclude(conversation => conversation.Members)
            .Include(message => message.UserStates)
            .Include(message => message.Reactions)
            .Include(message => message.MediaItems)
            .Where(message => ids.Contains(message.Id))
            .ToListAsync(cancellationToken);

        if (messages.Count != ids.Count)
            throw new NotFoundException("Seçilmiş mesajlardan biri tapılmadı.");

        var conversationIds = messages
            .Select(message => message.ConversationId)
            .Distinct()
            .ToList();
        if (conversationIds.Count != 1)
            throw new BadRequestException("Mesajlar eyni söhbətdən seçilməlidir.");

        var conversation = messages[0].Conversation;
        var isParticipant = conversation.IsGroup
            ? conversation.Members.Any(member => member.UserId == userId) &&
              messages.All(message => conversation.Members.Any(member =>
                  member.UserId == userId &&
                  message.SentAtUtc >= member.JoinedAtUtc))
            : conversation.UserOneId == userId || conversation.UserTwoId == userId;
        if (!isParticipant)
            throw new ForbiddenException("Bu söhbətə giriş yoxdur.");
        if (messages.Any(message => message.Kind == ChatMessageKind.System))
            throw new BadRequestException("Sistem mesajları silinə bilməz.");

        if (scope == ChatMessageDeleteScope.ForEveryone &&
            messages.Any(message => message.SenderUserId != userId))
        {
            throw new ForbiddenException("Başqa istifadəçinin mesajını hər kəsdən silmək olmaz.");
        }

        var replyTargetIds = await _context.ChatMessages
            .AsNoTracking()
            .Where(message =>
                message.ReplyToMessageId.HasValue &&
                ids.Contains(message.ReplyToMessageId.Value))
            .Select(message => message.ReplyToMessageId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var replyTargetSet = replyTargetIds.ToHashSet();

        var otherParticipantIds = conversation.IsGroup
            ? conversation.Members
                .Where(member => member.UserId != userId)
                .Select(member => member.UserId)
                .ToList()
            : new List<Guid>
            {
                (conversation.UserOneId == userId
                    ? conversation.UserTwoId
                    : conversation.UserOneId)!.Value
            };

        var now = DateTime.UtcNow;
        var mediaKeys = messages
            .SelectMany(message => message.MediaItems
                .Select(item => item.StorageKey)
                .Append(message.MediaStorageKey))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var message in messages)
        {
            if (scope == ChatMessageDeleteScope.ForEveryone)
            {
                if (!message.DeletedForEveryoneAtUtc.HasValue)
                    ClearMessageContent(message, now);
                continue;
            }

            var allOtherParticipantsDeleted = otherParticipantIds.All(participantId =>
                message.UserStates.Any(state =>
                    state.UserId == participantId && state.IsDeleted));

            if (allOtherParticipantsDeleted)
            {
                if (replyTargetSet.Contains(message.Id))
                    ClearMessageContent(message, now);
                else
                    _context.ChatMessages.Remove(message);
                continue;
            }

            var state = message.UserStates.FirstOrDefault(item => item.UserId == userId);
            if (state is null)
            {
                _context.ChatMessageUserStates.Add(new ChatMessageUserState
                {
                    MessageId = message.Id,
                    UserId = userId,
                    IsDeleted = true,
                    DeletedAtUtc = now
                });
            }
            else
            {
                state.IsDeleted = true;
                state.DeletedAtUtc ??= now;
                state.PinnedAtUtc = null;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var mediaKey in mediaKeys)
        {
            var stillReferencedByMessage = await _context.ChatMessages
                .AsNoTracking()
                .AnyAsync(message => message.MediaStorageKey == mediaKey, cancellationToken);
            var stillReferencedByCollection = await _context.ChatMessageMedia
                .AsNoTracking()
                .AnyAsync(item => item.StorageKey == mediaKey, cancellationToken);

            if (!stillReferencedByMessage && !stillReferencedByCollection)
                await _fileStorage.DeleteChatMediaAsync(mediaKey);
        }

        return new ChatBulkDeleteResultDTO
        {
            ConversationId = conversation.Id,
            Scope = scope,
            Items = ids.Select(messageId => new ChatMessageDeletedDTO
            {
                MessageId = messageId,
                ConversationId = conversation.Id,
                DeletedByUserId = userId,
                Scope = scope,
                DeletedAtUtc = now
            }).ToList()
        };
    }

    private async Task AddMemberAddedSystemMessagesAsync(
        ChatConversation group,
        Guid actorUserId,
        IReadOnlyCollection<Guid> addedUserIds,
        DateTime baseTimeUtc,
        CancellationToken cancellationToken)
    {
        var ids = addedUserIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            return;

        var userNames = await _context.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new { user.Id, UserName = user.UserName! })
            .ToDictionaryAsync(user => user.Id, user => user.UserName, cancellationToken);

        var participantIds = group.Members
            .Select(member => member.UserId)
            .Distinct()
            .ToList();
        var eventIndex = 0;

        foreach (var addedUserId in ids)
        {
            if (!userNames.TryGetValue(addedUserId, out var addedUserName))
                continue;

            var eventTime = baseTimeUtc.AddTicks(++eventIndex);
            var membership = group.Members.First(member => member.UserId == addedUserId);
            membership.JoinedAtUtc = eventTime;

            var systemMessage = new ChatMessage
            {
                Conversation = group,
                SenderUserId = actorUserId,
                Kind = ChatMessageKind.System,
                Text = addedUserName,
                SentAtUtc = eventTime
            };

            foreach (var participantId in participantIds.Where(id => id != actorUserId))
            {
                systemMessage.UserStates.Add(new ChatMessageUserState
                {
                    UserId = participantId,
                    IsDeleted = false
                });
            }

            group.Messages.Add(systemMessage);
        }

        group.UpdatedAtUtc = eventIndex > 0
            ? baseTimeUtc.AddTicks(eventIndex)
            : baseTimeUtc;
    }

    private async Task<ChatConversation> GetGroupForCreatorAsync(
        Guid userId,
        long conversationId,
        CancellationToken cancellationToken)
    {
        var group = await _context.ChatConversations
            .FirstOrDefaultAsync(conversation =>
                    conversation.Id == conversationId && conversation.IsGroup,
                cancellationToken)
            ?? throw new NotFoundException("Qrup tapılmadı.");

        if (group.InitiatedByUserId != userId)
            throw new ForbiddenException("Bu qrupun ayarlarını yalnız qrupu yaradan istifadəçi dəyişə bilər.");
        return group;
    }

    private async Task ValidateGroupMemberCandidatesAsync(
        IReadOnlyCollection<Guid> candidateIds,
        IReadOnlyCollection<Guid> allParticipantIds,
        CancellationToken cancellationToken)
    {
        var candidates = candidateIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (candidates.Count == 0)
            return;

        const int queryBatchSize = 500;
        var now = DateTime.UtcNow;
        var availableIdCount = 0;
        foreach (var candidateBatch in candidates.Chunk(queryBatchSize))
        {
            availableIdCount += await _context.Users
                .AsNoTracking()
                .CountAsync(user =>
                    candidateBatch.Contains(user.Id) &&
                    user.AccountDeletionRequestedAtUtc == null &&
                    !user.IsPermanentlyBanned &&
                    (!user.BannedUntilUtc.HasValue || user.BannedUntilUtc <= now),
                    cancellationToken);
        }

        if (availableIdCount != candidates.Count)
        {
            throw new BadRequestException(
                "Seçilmiş istifadəçilərdən biri tapılmadı və ya hazırda qrupa əlavə edilə bilməz.");
        }

        var participantSet = allParticipantIds
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        var candidateSet = candidates.ToHashSet();

        foreach (var candidateBatch in candidates.Chunk(queryBatchSize))
        {
            var relevantBlocks = await _context.UserBlocks
                .AsNoTracking()
                .Where(block =>
                    candidateBatch.Contains(block.BlockerId) ||
                    candidateBatch.Contains(block.BlockedUserId))
                .Select(block => new { block.BlockerId, block.BlockedUserId })
                .ToListAsync(cancellationToken);

            var hasBlockingRelationship = relevantBlocks.Any(block =>
                participantSet.Contains(block.BlockerId) &&
                participantSet.Contains(block.BlockedUserId) &&
                (candidateSet.Contains(block.BlockerId) ||
                 candidateSet.Contains(block.BlockedUserId)));

            if (hasBlockingRelationship)
            {
                throw new BadRequestException(
                    "Blok münasibəti olan istifadəçilər eyni qrupa əlavə edilə bilməz.");
            }
        }
    }

    private static string NormalizeGroupName(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 2 or > 100)
            throw new BadRequestException("Qrup adı 2-100 simvol aralığında olmalıdır.");
        return normalized;
    }

    private static string? NormalizeGroupDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        if (normalized.Length > 500)
            throw new BadRequestException("Qrup açıqlaması maksimum 500 simvol ola bilər.");
        return normalized;
    }
}
