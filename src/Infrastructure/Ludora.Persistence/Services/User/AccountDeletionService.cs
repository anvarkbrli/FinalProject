using System.Text.Json;

using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models.Notification;
using Ludora.Domain.Entities.Forum;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Ludora.Application.Extensions;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services
{
    public sealed class AccountDeletionService : IAccountDeletionService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly INotificationService _notificationService;
        private readonly IFileStorageService _fileStorageService;

        public AccountDeletionService(
            AppDbContext context,
            UserManager<AppUser> userManager,
            INotificationService notificationService,
            IFileStorageService fileStorageService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _fileStorageService = fileStorageService;
        }

        public async Task<DeleteAccountResultDTO> RequestDeletionAsync(
            Guid userId,
            DeleteAccountRequestDTO dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new NotFoundException("İstifadəçi tapılmadı.");

            if (!await _userManager.CheckPasswordAsync(user, dto.CurrentPassword))
                throw new BadRequestException("Cari şifrə yanlışdır.");

            var expectedConfirmation = user.UserName ?? string.Empty;
            if (!string.Equals(
                    dto.ConfirmationText.Trim(),
                    expectedConfirmation,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(
                    "Hesabı silmək üçün istifadəçi adınızı düzgün yazın.");
            }

            if (user.AccountDeletionRequestedAtUtc.HasValue)
            {
                var existingPendingCount = await _context.ForumCommunities
                    .IgnoreQueryFilters()
                    .CountAsync(x => x.OwnerId == userId, cancellationToken);

                return new DeleteAccountResultDTO
                {
                    DeletedImmediately = false,
                    PendingOwnershipTransfers = existingPendingCount > 0,
                    PendingCommunityCount = existingPendingCount,
                    Message = existingPendingCount > 0
                        ? "Hesab silinməsi artıq başladılıb. İcma sahiblik təklifləri tamamlandıqdan sonra hesab avtomatik silinəcək."
                        : "Hesab silinməsi artıq başladılıb."
                };
            }

            var files = new FileCleanupBatch();
            var otherOwnersToFinalize = new HashSet<Guid>();

            await using var transaction = await _context.Database
                .BeginTransactionAsync(cancellationToken);

            user.AccountDeletionRequestedAtUtc = DateTime.UtcNow;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.Bio = null;
            user.SocialLinksJson = null;
            user.SteamId = null;
            user.SteamDisplayName = null;
            user.SteamAvatarUrl = null;
            user.SteamProfileUrl = null;
            user.SteamLinkedAtUtc = null;
            user.SteamLastSyncedAtUtc = null;

            files.PublicFiles.Add(user.ProfileImageUrl);
            user.ProfileImageUrl = null;

            var communitiesWhereUserIsCandidate = await _context.ForumCommunities
                .IgnoreQueryFilters()
                .Where(x =>
                    x.PendingOwnerCandidateId == userId &&
                    x.OwnerId != userId)
                .ToListAsync(cancellationToken);

            foreach (var community in communitiesWhereUserIsCandidate)
            {
                var ownerToFinalize = await AdvanceOwnershipOfferForDepartingCandidateAsync(
                    community,
                    userId,
                    files,
                    cancellationToken);

                if (ownerToFinalize.HasValue)
                    otherOwnersToFinalize.Add(ownerToFinalize.Value);
            }

            var ownedCommunities = await _context.ForumCommunities
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.OwnerId == userId)
                .Select(x => new { x.Id, x.IsDeleted })
                .ToListAsync(cancellationToken);

            foreach (var ownedCommunity in ownedCommunities)
            {
                if (ownedCommunity.IsDeleted)
                {
                    files.Merge(await HardDeleteCommunityDataAsync(
                        ownedCommunity.Id,
                        cancellationToken));
                    continue;
                }

                var community = await _context.ForumCommunities
                    .IgnoreQueryFilters()
                    .FirstAsync(x => x.Id == ownedCommunity.Id, cancellationToken);

                community.DeleteIfOwnershipTransferFails = true;
                community.OwnershipTransferStartedAt ??= DateTime.UtcNow;
                community.OwnershipTransferDeclinedUserIdsJson ??= "[]";
                community.UpdatedAt = DateTime.UtcNow;

                var pendingCandidateIsUsable = community.PendingOwnerCandidateId.HasValue &&
                    await IsEligibleOwnershipCandidateAsync(
                        community.Id,
                        community.PendingOwnerCandidateId.Value,
                        userId,
                        cancellationToken);

                if (pendingCandidateIsUsable)
                    continue;

                var declinedIds = ReadDeclinedCandidateIds(community);
                if (community.PendingOwnerCandidateId.HasValue)
                    declinedIds.Add(community.PendingOwnerCandidateId.Value);

                var candidate = await FindNextOwnershipCandidateAsync(
                    community.Id,
                    userId,
                    declinedIds,
                    cancellationToken);

                if (candidate is null)
                {
                    files.Merge(await HardDeleteCommunityDataAsync(
                        community.Id,
                        cancellationToken));
                    continue;
                }

                community.PendingOwnerCandidateId = candidate.UserId;
                community.OwnershipTransferDeclinedUserIdsJson =
                    JsonSerializer.Serialize(declinedIds);

                await QueueOwnershipOfferAsync(
                    community,
                    userId,
                    candidate,
                    cancellationToken);
            }

            files.Merge(await PurgeUserDataAsync(
                userId,
                preserveOwnedCommunityMemberships: true,
                cancellationToken));

            await _context.SaveChangesAsync(cancellationToken);

            var pendingCommunityCount = await _context.ForumCommunities
                .IgnoreQueryFilters()
                .CountAsync(x => x.OwnerId == userId, cancellationToken);

            if (pendingCommunityCount == 0)
            {
                files.Merge(await PurgeUserDataAsync(
                    userId,
                    preserveOwnedCommunityMemberships: false,
                    cancellationToken));

                await DeleteIdentityUserAsync(user, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                await DeleteFilesBestEffortAsync(files);

                foreach (var ownerId in otherOwnersToFinalize)
                    await TryFinalizeAsync(ownerId, cancellationToken);

                return new DeleteAccountResultDTO
                {
                    DeletedImmediately = true,
                    PendingOwnershipTransfers = false,
                    PendingCommunityCount = 0,
                    Message = "Hesabınız və hesabla bağlı bütün məlumatlar həmişəlik silindi."
                };
            }

            await transaction.CommitAsync(cancellationToken);
            await DeleteFilesBestEffortAsync(files);

            foreach (var ownerId in otherOwnersToFinalize)
                await TryFinalizeAsync(ownerId, cancellationToken);

            return new DeleteAccountResultDTO
            {
                DeletedImmediately = false,
                PendingOwnershipTransfers = true,
                PendingCommunityCount = pendingCommunityCount,
                Message =
                    $"Hesab silinməsi başladıldı. {pendingCommunityCount} icma üçün sahiblik təklifi gözlənilir. Təkliflər tamamlandıqdan sonra hesab avtomatik və həmişəlik silinəcək."
            };
        }

        public async Task TryFinalizeAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

            if (user?.AccountDeletionRequestedAtUtc is null)
                return;

            var stillOwnsCommunity = await _context.ForumCommunities
                .IgnoreQueryFilters()
                .AnyAsync(x => x.OwnerId == userId, cancellationToken);

            if (stillOwnsCommunity)
                return;

            var files = new FileCleanupBatch();
            await using var transaction = await _context.Database
                .BeginTransactionAsync(cancellationToken);

            files.PublicFiles.Add(user.ProfileImageUrl);
            files.Merge(await PurgeUserDataAsync(
                userId,
                preserveOwnedCommunityMemberships: false,
                cancellationToken));

            await DeleteIdentityUserAsync(user, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await DeleteFilesBestEffortAsync(files);
        }

        public async Task HardDeleteCommunityAsync(
            int communityId,
            CancellationToken cancellationToken = default)
        {
            var files = await HardDeleteCommunityDataAsync(
                communityId,
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await DeleteFilesBestEffortAsync(files);
        }

        private async Task<FileCleanupBatch> PurgeUserDataAsync(
            Guid userId,
            bool preserveOwnedCommunityMemberships,
            CancellationToken cancellationToken)
        {
            var files = new FileCleanupBatch();

            var ownedPostIds = await _context.UserPosts
                .IgnoreQueryFilters()
                .Where(x => x.UserId == userId)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            files.PublicFiles.AddRange(await _context.UserPosts
                .IgnoreQueryFilters()
                .Where(x => x.UserId == userId && x.ImageUrl != null)
                .Select(x => x.ImageUrl)
                .ToListAsync(cancellationToken));

            await _context.PostLikes
                .Where(x => x.UserId == userId || ownedPostIds.Contains(x.PostId))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.PostComments
                .IgnoreQueryFilters()
                .Where(x => x.UserId == userId || ownedPostIds.Contains(x.PostId))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.UserPosts
                .IgnoreQueryFilters()
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await _context.UserPosts
                .IgnoreQueryFilters()
                .Where(x => x.DeletedByUserId == userId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.DeletedByUserId, (Guid?)null),
                    cancellationToken);

            var ownedReviewIds = await _context.Reviews
                .IgnoreQueryFilters()
                .Where(x => x.UserId == userId)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            await _context.ReviewLikes
                .Where(x => x.UserId == userId || ownedReviewIds.Contains(x.ReviewId))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.ReviewComments
                .IgnoreQueryFilters()
                .Where(x => x.UserId == userId || ownedReviewIds.Contains(x.ReviewId))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.Reviews
                .IgnoreQueryFilters()
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            var ownedListIds = await _context.GameLists
                .IgnoreQueryFilters()
                .Where(x => x.UserId == userId)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            await _context.GameListLikes
                .Where(x => x.UserId == userId || ownedListIds.Contains(x.GameListId))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.GameListComments
                .IgnoreQueryFilters()
                .Where(x => x.UserId == userId || ownedListIds.Contains(x.GameListId))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.GameLists
                .IgnoreQueryFilters()
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            var ownedDiscussionIds = await _context.Discussions
                .IgnoreQueryFilters()
                .Where(x => x.UserId == userId)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            files.Merge(await HardDeleteDiscussionsAsync(
                ownedDiscussionIds,
                cancellationToken));

            var ownReplyIds = await _context.DiscussionReplies
                .IgnoreQueryFilters()
                .Where(x => x.UserId == userId)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (ownReplyIds.Count > 0)
            {
                await _context.DiscussionReplies
                    .IgnoreQueryFilters()
                    .Where(x =>
                        x.ParentReplyId.HasValue &&
                        ownReplyIds.Contains(x.ParentReplyId.Value))
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(x => x.ParentReplyId, (int?)null),
                        cancellationToken);

                await _context.DiscussionReplyFollows
                    .Where(x => ownReplyIds.Contains(x.ReplyId) || x.UserId == userId)
                    .ExecuteDeleteAsync(cancellationToken);
                await _context.DiscussionReplyLikes
                    .Where(x => ownReplyIds.Contains(x.ReplyId) || x.UserId == userId)
                    .ExecuteDeleteAsync(cancellationToken);
                await _context.DiscussionReplyVotes
                    .Where(x => ownReplyIds.Contains(x.ReplyId) || x.UserId == userId)
                    .ExecuteDeleteAsync(cancellationToken);
                await _context.DiscussionReplies
                    .IgnoreQueryFilters()
                    .Where(x => x.UserId == userId)
                    .ExecuteDeleteAsync(cancellationToken);
            }
            else
            {
                await _context.DiscussionReplyFollows
                    .Where(x => x.UserId == userId)
                    .ExecuteDeleteAsync(cancellationToken);
                await _context.DiscussionReplyLikes
                    .Where(x => x.UserId == userId)
                    .ExecuteDeleteAsync(cancellationToken);
                await _context.DiscussionReplyVotes
                    .Where(x => x.UserId == userId)
                    .ExecuteDeleteAsync(cancellationToken);
            }

            await _context.DiscussionLikes
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.DiscussionSaves
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.DiscussionViews
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await _context.ForumCommunityFollows
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            var membershipQuery = _context.ForumCommunityMembers
                .IgnoreQueryFilters()
                .Where(x => x.UserId == userId);

            if (preserveOwnedCommunityMemberships)
            {
                var ownedCommunityIds = await _context.ForumCommunities
                    .IgnoreQueryFilters()
                    .Where(x => x.OwnerId == userId)
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken);

                membershipQuery = membershipQuery.Where(
                    x => !ownedCommunityIds.Contains(x.CommunityId));
            }

            await membershipQuery.ExecuteDeleteAsync(cancellationToken);

            await _context.UserFollows
                .Where(x => x.FollowerId == userId || x.FollowingId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.UserBlocks
                .Where(x => x.BlockerId == userId || x.BlockedUserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await _context.UserActivities
                .Where(x => x.UserId == userId || x.TargetUserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await _context.UserBadges
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.NotificationPreferences
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.Notifications
                .Where(x =>
                    x.RecipientUserId == userId ||
                    x.ActorUserId == userId &&
                     (!preserveOwnedCommunityMemberships ||
                      x.Type != NotificationType.CommunityOwnershipOffered))
                .ExecuteDeleteAsync(cancellationToken);

            // Direct conversations and groups created by the departing user are deleted
            // completely. Membership in groups created by somebody else is removed without
            // deleting the group itself.
            var conversationIds = await _context.ChatConversations
                .Where(x =>
                    x.UserOneId == userId ||
                    x.UserTwoId == userId ||
                    x.InitiatedByUserId == userId)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            files.PublicFiles.AddRange(await _context.ChatConversations
                .Where(x =>
                    conversationIds.Contains(x.Id) &&
                    x.IsGroup &&
                    x.GroupImageUrl != null)
                .Select(x => x.GroupImageUrl)
                .ToListAsync(cancellationToken));

            files.ChatFiles.AddRange(await _context.ChatMessages
                .Where(x =>
                    conversationIds.Contains(x.ConversationId) &&
                    x.MediaStorageKey != null)
                .Select(x => x.MediaStorageKey)
                .ToListAsync(cancellationToken));
            files.ChatFiles.AddRange(await _context.ChatMessageMedia
                .Where(x => conversationIds.Contains(x.Message.ConversationId))
                .Select(x => x.StorageKey)
                .ToListAsync(cancellationToken));

            var remainingGroupMessageIds = await _context.ChatMessages
                .Where(x =>
                    x.SenderUserId == userId &&
                    x.Conversation.IsGroup &&
                    !conversationIds.Contains(x.ConversationId))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (remainingGroupMessageIds.Count > 0)
            {
                files.ChatFiles.AddRange(await _context.ChatMessages
                    .Where(x =>
                        remainingGroupMessageIds.Contains(x.Id) &&
                        x.MediaStorageKey != null)
                    .Select(x => x.MediaStorageKey)
                    .ToListAsync(cancellationToken));
                files.ChatFiles.AddRange(await _context.ChatMessageMedia
                    .Where(x => remainingGroupMessageIds.Contains(x.MessageId))
                    .Select(x => x.StorageKey)
                    .ToListAsync(cancellationToken));

                // Preserve messages that replied to the departing user's messages. Their
                // reply preview is simply detached before the original messages are removed.
                await _context.ChatMessages
                    .Where(x =>
                        x.ReplyToMessageId.HasValue &&
                        remainingGroupMessageIds.Contains(x.ReplyToMessageId.Value))
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(x => x.ReplyToMessageId, (long?)null),
                        cancellationToken);

                await _context.ChatMessages
                    .Where(x => remainingGroupMessageIds.Contains(x.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // Remove per-user state from groups that remain after account deletion. These
            // relationships use restrictive foreign keys and otherwise prevent the Identity
            // user row from being removed.
            await _context.ChatMessageReactions
                .Where(x =>
                    x.UserId == userId &&
                    !conversationIds.Contains(x.Message.ConversationId))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.ChatMessageUserStates
                .Where(x =>
                    x.UserId == userId &&
                    !conversationIds.Contains(x.Message.ConversationId))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.ChatConversationUserStates
                .Where(x =>
                    x.UserId == userId &&
                    !conversationIds.Contains(x.ConversationId))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.ChatConversationMembers
                .Where(x =>
                    x.UserId == userId &&
                    !conversationIds.Contains(x.ConversationId))
                .ExecuteDeleteAsync(cancellationToken);

            await _context.ChatMessages
                .Where(x => conversationIds.Contains(x.ConversationId))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.ChatConversations
                .Where(x => conversationIds.Contains(x.Id))
                .ExecuteDeleteAsync(cancellationToken);

            await _context.SteamLinkChallenges
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.SteamFriendSnapshots
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.SteamOwnedGames
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.UserGamePlayLogs
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.UserGames
                .Where(x => x.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            var reportIds = await _context.UserReports
                .Where(x =>
                    x.ReporterUserId == userId ||
                    x.ReportedUserId == userId ||
                    x.ReviewedByUserId == userId)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            files.ReportFiles.AddRange(await _context.UserReports
                .Where(x =>
                    reportIds.Contains(x.Id) &&
                    x.EvidenceImageStorageKey != null)
                .Select(x => x.EvidenceImageStorageKey)
                .ToListAsync(cancellationToken));
            files.ReportFiles.AddRange(await _context.UserReportEvidenceImages
                .Where(x => reportIds.Contains(x.UserReportId))
                .Select(x => x.StorageKey)
                .ToListAsync(cancellationToken));

            var warningIds = await _context.UserWarnings
                .Where(x => x.UserId == userId || x.WarnedByUserId == userId)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            var banIds = await _context.UserBanHistories
                .Where(x =>
                    x.UserId == userId ||
                    x.BannedByUserId == userId ||
                    x.UnbannedByUserId == userId)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            await _context.ModeratorActionLogs
                .Where(x =>
                    x.AdminUserId == userId ||
                    x.TargetUserId == userId ||
                    x.UserReportId.HasValue && reportIds.Contains(x.UserReportId.Value) ||
                    x.UserWarningId.HasValue && warningIds.Contains(x.UserWarningId.Value) ||
                    x.UserBanHistoryId.HasValue && banIds.Contains(x.UserBanHistoryId.Value))
                .ExecuteDeleteAsync(cancellationToken);

            await _context.UserReports
                .Where(x => reportIds.Contains(x.Id))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.UserWarnings
                .Where(x => warningIds.Contains(x.Id))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.UserBanHistories
                .Where(x => banIds.Contains(x.Id))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.Announcements
                .IgnoreQueryFilters()
                .Where(x => x.CreatedByUserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await _context.Users
                .Where(x => x.BannedByUserId == userId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.BannedByUserId, (Guid?)null),
                    cancellationToken);

            return files;
        }

        private async Task<FileCleanupBatch> HardDeleteCommunityDataAsync(
            int communityId,
            CancellationToken cancellationToken)
        {
            var files = new FileCleanupBatch();
            var community = await _context.ForumCommunities
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.Id == communityId)
                .Select(x => new { x.IconUrl, x.CoverImageUrl })
                .FirstOrDefaultAsync(cancellationToken);

            if (community is null)
                return files;

            files.PublicFiles.Add(community.IconUrl);
            files.PublicFiles.Add(community.CoverImageUrl);

            var discussionIds = await _context.Discussions
                .IgnoreQueryFilters()
                .Where(x => x.CommunityId == communityId)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            files.Merge(await HardDeleteDiscussionsAsync(
                discussionIds,
                cancellationToken));

            await _context.ForumCommunityFollows
                .Where(x => x.CommunityId == communityId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.ForumCommunityMembers
                .IgnoreQueryFilters()
                .Where(x => x.CommunityId == communityId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.ForumCommunities
                .IgnoreQueryFilters()
                .Where(x => x.Id == communityId)
                .ExecuteDeleteAsync(cancellationToken);

            foreach (var entry in _context.ChangeTracker
                         .Entries<ForumCommunity>()
                         .Where(x => x.Entity.Id == communityId)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }

            return files;
        }

        private async Task<FileCleanupBatch> HardDeleteDiscussionsAsync(
            IReadOnlyCollection<int> discussionIds,
            CancellationToken cancellationToken)
        {
            var files = new FileCleanupBatch();
            if (discussionIds.Count == 0)
                return files;

            files.PublicFiles.AddRange(await _context.DiscussionMedia
                .Where(x => discussionIds.Contains(x.DiscussionId))
                .Select(x => x.Url)
                .ToListAsync(cancellationToken));

            var replyIds = await _context.DiscussionReplies
                .IgnoreQueryFilters()
                .Where(x => discussionIds.Contains(x.DiscussionId))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (replyIds.Count > 0)
            {
                await _context.DiscussionReplies
                    .IgnoreQueryFilters()
                    .Where(x => replyIds.Contains(x.Id))
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(x => x.ParentReplyId, (int?)null),
                        cancellationToken);
                await _context.DiscussionReplyFollows
                    .Where(x => replyIds.Contains(x.ReplyId))
                    .ExecuteDeleteAsync(cancellationToken);
                await _context.DiscussionReplyLikes
                    .Where(x => replyIds.Contains(x.ReplyId))
                    .ExecuteDeleteAsync(cancellationToken);
                await _context.DiscussionReplyVotes
                    .Where(x => replyIds.Contains(x.ReplyId))
                    .ExecuteDeleteAsync(cancellationToken);
                await _context.DiscussionReplies
                    .IgnoreQueryFilters()
                    .Where(x => replyIds.Contains(x.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            await _context.DiscussionLikes
                .Where(x => discussionIds.Contains(x.DiscussionId))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.DiscussionSaves
                .Where(x => discussionIds.Contains(x.DiscussionId))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.DiscussionViews
                .Where(x => discussionIds.Contains(x.DiscussionId))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.DiscussionMedia
                .Where(x => discussionIds.Contains(x.DiscussionId))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.Discussions
                .IgnoreQueryFilters()
                .Where(x => discussionIds.Contains(x.Id))
                .ExecuteDeleteAsync(cancellationToken);

            return files;
        }

        private async Task<Guid?> AdvanceOwnershipOfferForDepartingCandidateAsync(
            ForumCommunity community,
            Guid departingUserId,
            FileCleanupBatch files,
            CancellationToken cancellationToken)
        {
            var declinedIds = ReadDeclinedCandidateIds(community);
            declinedIds.Add(departingUserId);

            var nextCandidate = await FindNextOwnershipCandidateAsync(
                community.Id,
                community.OwnerId,
                declinedIds,
                cancellationToken);

            if (nextCandidate is null)
            {
                if (community.DeleteIfOwnershipTransferFails)
                {
                    var ownerId = community.OwnerId;
                    files.Merge(await HardDeleteCommunityDataAsync(
                        community.Id,
                        cancellationToken));
                    return ownerId;
                }

                ClearOwnershipTransfer(community);
                community.UpdatedAt = DateTime.UtcNow;

                await _notificationService.QueueAsync(
                    new NotificationCreateRequest
                    {
                        RecipientUserId = community.OwnerId,
                        ActorUserId = null,
                        Type = NotificationType.CommunityOwnershipOffered,
                        EntityType = NotificationEntityType.None,
                        EntityId = community.Id.ToString(),
                        Title = "Sahiblik ötürülmədi",
                        Message = $"{community.Name} icmasında sahiblik təklifini qəbul edən üzv tapılmadı.",
                        ActionUrl = $"/forum/communities/{community.Slug}",
                        BypassPreferences = true
                    },
                    cancellationToken);

                return null;
            }

            community.PendingOwnerCandidateId = nextCandidate.UserId;
            community.OwnershipTransferDeclinedUserIdsJson =
                JsonSerializer.Serialize(declinedIds);
            community.UpdatedAt = DateTime.UtcNow;

            await QueueOwnershipOfferAsync(
                community,
                community.OwnerId,
                nextCandidate,
                cancellationToken);

            return null;
        }

        private async Task<bool> IsEligibleOwnershipCandidateAsync(
            int communityId,
            Guid candidateId,
            Guid ownerId,
            CancellationToken cancellationToken)
        {
            return await _context.ForumCommunityMembers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(x =>
                    x.CommunityId == communityId &&
                    x.UserId == candidateId &&
                    x.UserId != ownerId &&
                    x.Status == ForumCommunityMembershipStatus.Active &&
                    !x.IsDeleted &&
                    x.User.AccountDeletionRequestedAtUtc == null,
                    cancellationToken);
        }

        private async Task<OwnershipCandidate?> FindNextOwnershipCandidateAsync(
            int communityId,
            Guid ownerId,
            IReadOnlyCollection<Guid> excludedUserIds,
            CancellationToken cancellationToken)
        {
            var query = _context.ForumCommunityMembers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x =>
                    x.CommunityId == communityId &&
                    x.UserId != ownerId &&
                    x.Status == ForumCommunityMembershipStatus.Active &&
                    !x.IsDeleted &&
                    x.User.AccountDeletionRequestedAtUtc == null);

            if (excludedUserIds.Count > 0)
                query = query.Where(x => !excludedUserIds.Contains(x.UserId));

            return await query
                .OrderBy(x => x.Role == ForumCommunityMemberRole.Moderator ? 0 : 1)
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .Select(x => new OwnershipCandidate(
                    x.UserId,
                    x.User.UserName ?? string.Empty))
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task QueueOwnershipOfferAsync(
            ForumCommunity community,
            Guid ownerId,
            OwnershipCandidate candidate,
            CancellationToken cancellationToken)
        {
            await _notificationService.QueueAsync(
                new NotificationCreateRequest
                {
                    RecipientUserId = candidate.UserId,
                    ActorUserId = ownerId,
                    Type = NotificationType.CommunityOwnershipOffered,
                    Priority = NotificationPriority.High,
                    EntityType = NotificationEntityType.None,
                    EntityId = community.Id.ToString(),
                    Title = "İcma sahibliyi təklifi",
                    Message = $"{community.Name} icmasının sahibi olmaq üçün təklif aldınız.",
                    ActionUrl = $"/forum/communities/{community.Slug}",
                    DataJson = JsonSerializer.Serialize(new
                    {
                        communityId = community.Id,
                        communitySlug = community.Slug,
                        candidateUserName = candidate.UserName
                    }),
                    BypassPreferences = true
                },
                cancellationToken);
        }

        private async Task DeleteIdentityUserAsync(
            AppUser user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _userManager.DeleteAsync(user);
            result.ThrowIfFailed();
        }

        private async Task DeleteFilesBestEffortAsync(FileCleanupBatch files)
        {
            foreach (var path in files.PublicFiles.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
            {
                try { await _fileStorageService.DeleteAsync(path); }
                catch { /* Database deletion must not be rolled back by an orphaned file. */ }
            }

            foreach (var key in files.ChatFiles.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
            {
                try
                {
                    var stillReferenced = await _context.ChatMessages
                        .AsNoTracking()
                        .AnyAsync(message => message.MediaStorageKey == key) ||
                        await _context.ChatMessageMedia
                            .AsNoTracking()
                            .AnyAsync(media => media.StorageKey == key);
                    if (!stillReferenced)
                        await _fileStorageService.DeleteChatMediaAsync(key);
                }
                catch { }
            }

            foreach (var key in files.ReportFiles.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
            {
                try { await _fileStorageService.DeleteReportEvidenceImageAsync(key); }
                catch { }
            }
        }

        private static HashSet<Guid> ReadDeclinedCandidateIds(ForumCommunity community)
        {
            if (string.IsNullOrWhiteSpace(community.OwnershipTransferDeclinedUserIdsJson))
                return [];

            try
            {
                return JsonSerializer.Deserialize<HashSet<Guid>>(
                        community.OwnershipTransferDeclinedUserIdsJson)
                    ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        private static void ClearOwnershipTransfer(ForumCommunity community)
        {
            community.PendingOwnerCandidateId = null;
            community.OwnershipTransferStartedAt = null;
            community.OwnershipTransferDeclinedUserIdsJson = null;
            community.DeleteIfOwnershipTransferFails = false;
        }

        private sealed record OwnershipCandidate(Guid UserId, string UserName);

        private sealed class FileCleanupBatch
        {
            public List<string?> PublicFiles { get; } = [];
            public List<string?> ChatFiles { get; } = [];
            public List<string?> ReportFiles { get; } = [];

            public void Merge(FileCleanupBatch other)
            {
                PublicFiles.AddRange(other.PublicFiles);
                ChatFiles.AddRange(other.ChatFiles);
                ReportFiles.AddRange(other.ReportFiles);
            }
        }
    }
}
