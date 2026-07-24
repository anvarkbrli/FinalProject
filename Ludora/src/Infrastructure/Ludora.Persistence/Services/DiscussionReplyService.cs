using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models.Notification;
using Ludora.Domain.Entities.Forum;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class DiscussionReplyService : IDiscussionReplyService
{
    private const int MaxContentLength = 5000;
    private const int MaxPageSize = 100;

    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;

    public DiscussionReplyService(
        AppDbContext context,
        INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<DiscussionReplyDTO> GetByIdAsync(
        int replyId,
        Guid? currentUserId,
        bool canModerate,
        CancellationToken cancellationToken = default)
    {
        replyId.EnsureValidId("Cavab ID-si");

        var replyInfo = await _context.DiscussionReplies
            .AsNoTracking()
            .Where(x => x.Id == replyId)
            .Select(x => new
            {
                ReplyAuthorId = x.UserId,
                DiscussionAuthorId = x.Discussion.UserId,
                CommunityVisibility = x.Discussion.CommunityId.HasValue
                    ? (ForumCommunityVisibility?)x.Discussion.Community!.Visibility
                    : null,
                IsCommunityMember = currentUserId.HasValue &&
                    x.Discussion.CommunityId.HasValue &&
                    x.Discussion.Community!.Members.Any(member =>
                        member.UserId == currentUserId.Value &&
                        member.Status == ForumCommunityMembershipStatus.Active)
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Cavab tapılmadı.");

        if (replyInfo.CommunityVisibility == ForumCommunityVisibility.Private &&
            !canModerate &&
            !replyInfo.IsCommunityMember)
        {
            throw new NotFoundException("Cavab tapılmadı.");
        }

        if (currentUserId.HasValue && !canModerate)
        {
            await EnsureUsersAreNotBlockedAsync(
                currentUserId.Value,
                replyInfo.DiscussionAuthorId,
                cancellationToken);

            await EnsureUsersAreNotBlockedAsync(
                currentUserId.Value,
                replyInfo.ReplyAuthorId,
                cancellationToken);
        }

        return await ProjectReplies(
                _context.DiscussionReplies.AsNoTracking().Where(x => x.Id == replyId),
                currentUserId,
                canModerate)
            .FirstAsync(cancellationToken);
    }

    public async Task<PagedResultDTO<DiscussionReplyDTO>> GetByDiscussionAsync(
        int discussionId,
        int pageNumber,
        int pageSize,
        Guid? currentUserId,
        bool canModerate,
        CancellationToken cancellationToken = default)
    {
        discussionId.EnsureValidId("Mövzu ID-si");
        ValidatePagination(pageNumber, pageSize);

        var discussionInfo = await _context.Discussions
            .AsNoTracking()
            .Where(x => x.Id == discussionId)
            .Select(x => new
            {
                x.UserId,
                CommunityVisibility = x.CommunityId.HasValue
                    ? (ForumCommunityVisibility?)x.Community!.Visibility
                    : null,
                IsCommunityMember = currentUserId.HasValue &&
                    x.CommunityId.HasValue &&
                    x.Community!.Members.Any(member =>
                        member.UserId == currentUserId.Value &&
                        member.Status == ForumCommunityMembershipStatus.Active)
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Mövzu tapılmadı.");

        if (discussionInfo.CommunityVisibility == ForumCommunityVisibility.Private &&
            !canModerate &&
            !discussionInfo.IsCommunityMember)
        {
            throw new NotFoundException("Mövzu tapılmadı.");
        }

        if (currentUserId.HasValue && !canModerate)
        {
            await EnsureUsersAreNotBlockedAsync(
                currentUserId.Value,
                discussionInfo.UserId,
                cancellationToken);
        }

        var query = _context.DiscussionReplies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.DiscussionId == discussionId);

        if (currentUserId.HasValue && !canModerate)
            query = ExcludeBlockedAuthors(query, currentUserId.Value);

        var flatItems = await ProjectReplies(query, currentUserId, canModerate)
            .ToListAsync(cancellationToken);

        var tree = BuildReplyTree(flatItems);
        var totalCount = tree.Count;
        var items = tree
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return CreatePagedResult(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PagedResultDTO<DiscussionReplyDTO>> GetDeletedAsync(
        int? discussionId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (discussionId.HasValue)
            discussionId.Value.EnsureValidId("Mövzu ID-si");

        ValidatePagination(pageNumber, pageSize);

        var query = _context.DiscussionReplies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.IsDeleted);

        if (discussionId.HasValue)
            query = query.Where(x => x.DiscussionId == discussionId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await ProjectReplies(query, null, true)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return CreatePagedResult(items, pageNumber, pageSize, totalCount);
    }

    public async Task<DiscussionReplyDTO> CreateAsync(
        int discussionId,
        Guid userId,
        DiscussionReplyCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        discussionId.EnsureValidId("Mövzu ID-si");
        ArgumentNullException.ThrowIfNull(dto);

        var discussion = await _context.Discussions
            .AsNoTracking()
            .Where(x => x.Id == discussionId)
            .Select(x => new
            {
                x.UserId,
                x.Slug,
                x.IsLocked,
                x.CommunityId,
                CommunityVisibility = x.CommunityId.HasValue
                    ? (ForumCommunityVisibility?)x.Community!.Visibility
                    : null,
                IsCommunityMember = x.CommunityId.HasValue &&
                    x.Community!.Members.Any(member =>
                        member.UserId == userId &&
                        member.Status == ForumCommunityMembershipStatus.Active)
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Mövzu tapılmadı.");

        if (discussion.IsLocked)
            throw new ConflictException("Bu mövzu kilidlənib və yeni cavab qəbul etmir.");

        if (discussion.CommunityVisibility == ForumCommunityVisibility.Private &&
            !discussion.IsCommunityMember)
        {
            throw new NotFoundException("Mövzu tapılmadı.");
        }

        await EnsureUsersAreNotBlockedAsync(userId, discussion.UserId, cancellationToken);

        var content = dto.Content.NormalizeRequiredText("Cavab mətni", MaxContentLength);
        Guid[] followedReplyRecipientIds = [];

        if (dto.ParentReplyId.HasValue)
        {
            dto.ParentReplyId.Value.EnsureValidId("Ana cavab ID-si");

            var parent = await _context.DiscussionReplies
                .AsNoTracking()
                .Where(x => x.Id == dto.ParentReplyId.Value)
                .Select(x => new
                {
                    x.DiscussionId,
                    x.ParentReplyId
                })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Cavab verilən şərh tapılmadı.");

            if (parent.DiscussionId != discussionId)
                throw new BadRequestException(
                    "Cavab yalnız eyni müzakirədəki şərhə yazıla bilər.");

            var depth = 1;
            var nextParentId = parent.ParentReplyId;
            while (nextParentId.HasValue)
            {
                depth++;
                if (depth >= 8)
                    throw new BadRequestException(
                        "Şərhlər maksimum 8 səviyyə iç-içə ola bilər.");

                nextParentId = await _context.DiscussionReplies
                    .AsNoTracking()
                    .Where(x => x.Id == nextParentId.Value)
                    .Select(x => x.ParentReplyId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            followedReplyRecipientIds = await _context.DiscussionReplyFollows
                .AsNoTracking()
                .Where(x => x.ReplyId == dto.ParentReplyId.Value)
                .Select(x => x.UserId)
                .ToArrayAsync(cancellationToken);
        }

        var reply = new DiscussionReply
        {
            DiscussionId = discussionId,
            UserId = userId,
            ParentReplyId = dto.ParentReplyId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        await _context.DiscussionReplies.AddAsync(reply, cancellationToken);

        await _notificationService.QueueAsync(
            new NotificationCreateRequest
            {
                RecipientUserId = discussion.UserId,
                ActorUserId = userId,
                Type = NotificationType.DiscussionReplied,
                EntityType = NotificationEntityType.Discussion,
                EntityId = discussionId.ToString(),
                Title = "Müzakirənizə cavab yazıldı",
                Message = "Açdığınız müzakirəyə cavab yazdı.",
                ActionUrl = $"/forum/discussions/{discussion.Slug}"
            },
            cancellationToken);

        foreach (var recipientUserId in followedReplyRecipientIds)
        {
            if (recipientUserId == discussion.UserId)
                continue;

            await _notificationService.QueueAsync(
                new NotificationCreateRequest
                {
                    RecipientUserId = recipientUserId,
                    ActorUserId = userId,
                    Type = NotificationType.FollowedDiscussionReplyActivity,
                    EntityType = NotificationEntityType.DiscussionReply,
                    EntityId = dto.ParentReplyId!.Value.ToString(),
                    Title = "İzlədiyiniz şərhə yeni cavab yazıldı",
                    Message = "İzlədiyiniz forum şərhinə yeni cavab əlavə etdi.",
                    ActionUrl = $"/forum/discussions/{discussion.Slug}"
                },
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await ProjectReplies(
                _context.DiscussionReplies.AsNoTracking().Where(x => x.Id == reply.Id),
                userId,
                false)
            .FirstAsync(cancellationToken);
    }

    public async Task<DiscussionReplyDTO> UpdateAsync(
        int replyId,
        Guid userId,
        bool canModerate,
        DiscussionReplyUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        replyId.EnsureValidId("Cavab ID-si");
        ArgumentNullException.ThrowIfNull(dto);

        if (!canModerate)
        {
            await EnsureReplyCommunityAccessAsync(
                replyId,
                userId,
                cancellationToken);
        }

        var reply = await _context.DiscussionReplies
            .Include(x => x.Discussion)
            .FirstOrDefaultAsync(x => x.Id == replyId, cancellationToken)
            ?? throw new NotFoundException("Cavab tapılmadı.");

        EnsureCanModify(reply.UserId, userId, canModerate);

        if (reply.Discussion.IsLocked && !canModerate)
            throw new ConflictException("Kilidlənmiş mövzudakı cavabı dəyişmək mümkün deyil.");

        reply.Content = dto.Content.NormalizeRequiredText("Cavab mətni", MaxContentLength);
        reply.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return await ProjectReplies(
                _context.DiscussionReplies.AsNoTracking().Where(x => x.Id == reply.Id),
                userId,
                canModerate)
            .FirstAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int replyId,
        Guid userId,
        bool canModerate,
        CancellationToken cancellationToken = default)
    {
        replyId.EnsureValidId("Cavab ID-si");

        if (!canModerate)
        {
            await EnsureReplyCommunityAccessAsync(
                replyId,
                userId,
                cancellationToken);
        }

        var reply = await _context.DiscussionReplies
            .Include(x => x.Discussion)
            .FirstOrDefaultAsync(x => x.Id == replyId, cancellationToken)
            ?? throw new NotFoundException("Cavab tapılmadı.");

        if (reply.UserId != userId &&
            reply.Discussion.UserId != userId &&
            !canModerate)
        {
            throw new ForbiddenException(
                "Bu şərhi silmək üçün icazəniz yoxdur.");
        }

        reply.IsDeleted = true;
        reply.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task LikeAsync(
        int replyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        replyId.EnsureValidId("Cavab ID-si");

        await EnsureReplyCommunityAccessAsync(
            replyId,
            userId,
            cancellationToken);

        var replyInfo = await _context.DiscussionReplies
            .AsNoTracking()
            .Where(x => x.Id == replyId)
            .Select(x => new
            {
                ReplyAuthorId = x.UserId,
                DiscussionAuthorId = x.Discussion.UserId
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Cavab tapılmadı.");

        await EnsureUsersAreNotBlockedAsync(
            userId,
            replyInfo.DiscussionAuthorId,
            cancellationToken);

        await EnsureUsersAreNotBlockedAsync(
            userId,
            replyInfo.ReplyAuthorId,
            cancellationToken);

        var alreadyLiked = await _context.DiscussionReplyLikes
            .AnyAsync(x => x.ReplyId == replyId && x.UserId == userId, cancellationToken);

        if (alreadyLiked)
            throw new ConflictException("Bu cavabı artıq bəyənmisiniz.");

        await _context.DiscussionReplyLikes.AddAsync(new DiscussionReplyLike
        {
            ReplyId = replyId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _notificationService.QueueAsync(
            new NotificationCreateRequest
            {
                RecipientUserId = replyInfo.ReplyAuthorId,
                ActorUserId = userId,
                Type = NotificationType.DiscussionReplyLiked,
                Priority = NotificationPriority.Low,
                EntityType = NotificationEntityType.DiscussionReply,
                EntityId = replyId.ToString(),
                Title = "Cavabınız bəyənildi",
                Message = "Müzakirədəki cavabınızı bəyəndi.",
                ActionUrl = "/#forum"
            },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UnlikeAsync(
        int replyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        replyId.EnsureValidId("Cavab ID-si");

        await EnsureReplyCommunityAccessAsync(
            replyId,
            userId,
            cancellationToken);

        var like = await _context.DiscussionReplyLikes
            .FirstOrDefaultAsync(
                x => x.ReplyId == replyId && x.UserId == userId,
                cancellationToken)
            ?? throw new NotFoundException("Cavab bəyənməsi tapılmadı.");

        _context.DiscussionReplyLikes.Remove(like);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DiscussionReplyDTO> SetVoteAsync(
        int replyId,
        Guid userId,
        short value,
        bool canModerate,
        CancellationToken cancellationToken = default)
    {
        replyId.EnsureValidId("Cavab ID-si");

        if (value is not (-1 or 0 or 1))
            throw new BadRequestException(
                "Səs dəyəri yalnız -1, 0 və ya 1 ola bilər.");

        var replyInfo = await _context.DiscussionReplies
            .AsNoTracking()
            .Where(x => x.Id == replyId)
            .Select(x => new
            {
                ReplyAuthorId = x.UserId,
                DiscussionAuthorId = x.Discussion.UserId,
                DiscussionSlug = x.Discussion.Slug
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Cavab tapılmadı.");

        await EnsureUsersAreNotBlockedAsync(
            userId,
            replyInfo.DiscussionAuthorId,
            cancellationToken);
        await EnsureUsersAreNotBlockedAsync(
            userId,
            replyInfo.ReplyAuthorId,
            cancellationToken);

        var vote = await _context.DiscussionReplyVotes
            .FirstOrDefaultAsync(
                x => x.ReplyId == replyId && x.UserId == userId,
                cancellationToken);
        var previousValue = vote?.Value ?? 0;

        if (value == 0)
        {
            if (vote is not null)
                _context.DiscussionReplyVotes.Remove(vote);
        }
        else if (vote is null)
        {
            await _context.DiscussionReplyVotes.AddAsync(
                new DiscussionReplyVote
                {
                    ReplyId = replyId,
                    UserId = userId,
                    Value = value,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken);
        }
        else if (vote.Value != value)
        {
            vote.Value = value;
            vote.UpdatedAt = DateTime.UtcNow;
        }

        if (value == 1 && previousValue != 1)
        {
            await _notificationService.QueueAsync(
                new NotificationCreateRequest
                {
                    RecipientUserId = replyInfo.ReplyAuthorId,
                    ActorUserId = userId,
                    Type = NotificationType.DiscussionReplyLiked,
                    Priority = NotificationPriority.Low,
                    EntityType = NotificationEntityType.DiscussionReply,
                    EntityId = replyId.ToString(),
                    Title = "Şərhinizə müsbət səs verildi",
                    Message = "Müzakirədəki şərhinizə müsbət səs verdi.",
                    ActionUrl = $"/forum/discussions/{replyInfo.DiscussionSlug}"
                },
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await ProjectReplies(
                _context.DiscussionReplies
                    .AsNoTracking()
                    .Where(x => x.Id == replyId),
                userId,
                canModerate)
            .FirstAsync(cancellationToken);
    }

    public async Task FollowAsync(
        int replyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        replyId.EnsureValidId("Şərh ID-si");

        await EnsureReplyCommunityAccessAsync(
            replyId,
            userId,
            cancellationToken);

        var replyInfo = await _context.DiscussionReplies
            .AsNoTracking()
            .Where(x => x.Id == replyId)
            .Select(x => new
            {
                ReplyAuthorId = x.UserId,
                DiscussionAuthorId = x.Discussion.UserId
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Şərh tapılmadı.");

        await EnsureUsersAreNotBlockedAsync(
            userId,
            replyInfo.DiscussionAuthorId,
            cancellationToken);
        await EnsureUsersAreNotBlockedAsync(
            userId,
            replyInfo.ReplyAuthorId,
            cancellationToken);

        var alreadyFollowing = await _context.DiscussionReplyFollows
            .AnyAsync(
                x => x.ReplyId == replyId && x.UserId == userId,
                cancellationToken);

        if (alreadyFollowing)
            return;

        await _context.DiscussionReplyFollows.AddAsync(
            new DiscussionReplyFollow
            {
                ReplyId = replyId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UnfollowAsync(
        int replyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        replyId.EnsureValidId("Şərh ID-si");

        var follow = await _context.DiscussionReplyFollows
            .FirstOrDefaultAsync(
                x => x.ReplyId == replyId && x.UserId == userId,
                cancellationToken);

        if (follow is null)
            return;

        _context.DiscussionReplyFollows.Remove(follow);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DiscussionReplyDTO> RestoreAsync(
        int replyId,
        CancellationToken cancellationToken = default)
    {
        replyId.EnsureValidId("Cavab ID-si");

        var reply = await _context.DiscussionReplies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == replyId, cancellationToken)
            ?? throw new NotFoundException("Cavab tapılmadı.");

        var discussionIsActive = await _context.Discussions
            .AsNoTracking()
            .AnyAsync(x => x.Id == reply.DiscussionId, cancellationToken);

        if (!discussionIsActive)
        {
            throw new ConflictException(
                "Cavabın aid olduğu mövzu silinib. Əvvəlcə mövzunu bərpa edin.");
        }

        if (reply.IsDeleted)
        {
            reply.IsDeleted = false;
            reply.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await ProjectReplies(
                _context.DiscussionReplies.AsNoTracking().Where(x => x.Id == replyId),
                null,
                true)
            .FirstAsync(cancellationToken);
    }

    private IQueryable<DiscussionReply> ExcludeBlockedAuthors(
        IQueryable<DiscussionReply> query,
        Guid currentUserId)
    {
        var blockedUserIds = _context.UserBlocks
            .AsNoTracking()
            .Where(x => x.BlockerId == currentUserId)
            .Select(x => x.BlockedUserId)
            .Concat(
                _context.UserBlocks
                    .AsNoTracking()
                    .Where(x => x.BlockedUserId == currentUserId)
                    .Select(x => x.BlockerId));

        return query.Where(x => !blockedUserIds.Contains(x.UserId));
    }

    private static IQueryable<DiscussionReplyDTO> ProjectReplies(
        IQueryable<DiscussionReply> query,
        Guid? currentUserId,
        bool canModerate)
    {
        return query.Select(x => new DiscussionReplyDTO
        {
            Id = x.Id,
            DiscussionId = x.DiscussionId,
            ParentReplyId = x.ParentReplyId,
            AuthorId = x.UserId,
            AuthorUserName = x.User.UserName ?? string.Empty,
            AuthorProfileImageUrl = x.User.ProfileImageUrl,
            Content = x.IsDeleted ? string.Empty : x.Content,
            NetScore = x.Votes.Sum(vote => (int?)vote.Value) ?? 0,
            CurrentUserVote = currentUserId.HasValue
                ? x.Votes
                    .Where(vote => vote.UserId == currentUserId.Value)
                    .Select(vote => (short?)vote.Value)
                    .FirstOrDefault() ?? 0
                : (short)0,
            IsFollowedByCurrentUser = currentUserId.HasValue &&
                                      x.Followers.Any(
                                          follow => follow.UserId == currentUserId.Value),
            ChildCount = x.ChildReplies.Count,
            IsDeleted = x.IsDeleted,
            CanEdit = !x.IsDeleted &&
                      currentUserId.HasValue &&
                      (canModerate || x.UserId == currentUserId.Value),
            CanDelete = currentUserId.HasValue &&
                        !x.IsDeleted &&
                        (canModerate ||
                         x.UserId == currentUserId.Value ||
                         x.Discussion.UserId == currentUserId.Value),
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        });
    }

    private static IReadOnlyList<DiscussionReplyDTO> BuildReplyTree(
        IReadOnlyList<DiscussionReplyDTO> flatItems)
    {
        var itemIds = flatItems.Select(x => x.Id).ToHashSet();
        var childrenByParent = flatItems
            .Where(x => x.ParentReplyId.HasValue)
            .ToLookup(x => x.ParentReplyId!.Value);

        DiscussionReplyDTO AttachChildren(
            DiscussionReplyDTO item,
            HashSet<int> path)
        {
            if (!path.Add(item.Id))
                return item with { Children = [] };

            var children = childrenByParent[item.Id]
                .Where(child => !path.Contains(child.Id))
                .OrderByDescending(child => child.NetScore)
                .ThenBy(child => child.CreatedAt)
                .Select(child =>
                    AttachChildren(child, new HashSet<int>(path)))
                .ToList();

            return item with
            {
                Children = children,
                ChildCount = children.Count
            };
        }

        return flatItems
            .Where(x =>
                !x.ParentReplyId.HasValue ||
                !itemIds.Contains(x.ParentReplyId.Value))
            .OrderByDescending(x => x.NetScore)
            .ThenBy(x => x.CreatedAt)
            .Select(x => AttachChildren(x, []))
            .ToList();
    }

    private async Task EnsureUsersAreNotBlockedAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken)
    {
        if (firstUserId == secondUserId)
            return;

        var isBlocked = await _context.UserBlocks
            .AsNoTracking()
            .AnyAsync(x =>
                    (x.BlockerId == firstUserId && x.BlockedUserId == secondUserId) ||
                    (x.BlockerId == secondUserId && x.BlockedUserId == firstUserId),
                cancellationToken);

        if (isBlocked)
            throw new ForbiddenException("Bloklanmış istifadəçilər arasında bu əməliyyat mümkün deyil.");
    }

    private async Task EnsureReplyCommunityAccessAsync(
        int replyId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var access = await _context.DiscussionReplies
            .AsNoTracking()
            .Where(x => x.Id == replyId)
            .Select(x => new
            {
                CommunityVisibility = x.Discussion.CommunityId.HasValue
                    ? (ForumCommunityVisibility?)x.Discussion.Community!.Visibility
                    : null,
                IsCommunityMember = !x.Discussion.CommunityId.HasValue ||
                    x.Discussion.Community!.Members.Any(member =>
                        member.UserId == userId &&
                        member.Status == ForumCommunityMembershipStatus.Active)
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Cavab tapılmadı.");

        if (access.CommunityVisibility == ForumCommunityVisibility.Private &&
            !access.IsCommunityMember)
        {
            throw new NotFoundException("Cavab tapılmadı.");
        }
    }

    private static void EnsureCanModify(
        Guid ownerId,
        Guid currentUserId,
        bool canModerate)
    {
        if (ownerId != currentUserId && !canModerate)
            throw new ForbiddenException("Bu cavabı dəyişmək üçün icazəniz yoxdur.");
    }

    private static void ValidatePagination(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");

        if (pageSize is < 1 or > MaxPageSize)
            throw new BadRequestException($"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");

        if ((long)(pageNumber - 1) * pageSize > int.MaxValue)
            throw new BadRequestException("Səhifə parametrləri həddindən artıq böyükdür.");
    }

    private static PagedResultDTO<T> CreatePagedResult<T>(
        IReadOnlyList<T> items,
        int pageNumber,
        int pageSize,
        int totalCount)
    {
        return new PagedResultDTO<T>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
}
