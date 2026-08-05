using System.Data;
using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Helpers;
using Ludora.Application.Interfaces;
using Ludora.Application.Models.Notification;
using Ludora.Domain.Entities.Forum;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class DiscussionService : IDiscussionService
{
    private const int MaxTitleLength = 200;
    private const int MaxContentLength = 10000;
    private const int MaxSearchLength = 200;
    private const int MaxPageSize = 100;
    private const int MaxMediaCount = 8;
    private const int MaxPollQuestionLength = 300;
    private const int MaxPollOptionLength = 120;
    private static readonly TimeSpan MaxPollDuration = TimeSpan.FromDays(30);

    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IFileStorageService _fileStorageService;

    public DiscussionService(
        AppDbContext context,
        INotificationService notificationService,
        IFileStorageService fileStorageService)
    {
        _context = context;
        _notificationService = notificationService;
        _fileStorageService = fileStorageService;
    }

    public async Task<PagedResultDTO<DiscussionListItemDTO>> GetAllAsync(
        DiscussionFilterDTO filter,
        Guid? currentUserId,
        bool canModerate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ValidatePagination(filter.PageNumber, filter.PageSize);

        var search = filter.Search.NormalizeOptionalText("AxtarÄ±ÅŸ mÉ™tni", MaxSearchLength);
        var categorySlug = filter.CategorySlug
            .NormalizeOptionalText("Kateqoriya slug-u", 120)?
            .ToLowerInvariant();
        var communitySlug = filter.CommunitySlug
            .NormalizeOptionalText("Ä°cma slug-u", 80)?
            .ToLowerInvariant();

        var query = _context.Discussions.AsNoTracking();

        if (communitySlug is null)
        {
            query = query.Where(x =>
                x.CommunityId == null ||
                x.Community!.Visibility != ForumCommunityVisibility.Private);
        }
        else
        {
            query = query.Where(x => x.Community != null &&
                                     x.Community.Slug == communitySlug);

            query = query.Where(x =>
                x.Community!.Visibility != ForumCommunityVisibility.Private ||
                currentUserId.HasValue &&
                 x.Community.Members.Any(member =>
                     member.UserId == currentUserId.Value &&
                     member.Status == ForumCommunityMembershipStatus.Active));
        }

        if (currentUserId.HasValue && !canModerate)
            query = ExcludeBlockedAuthors(query, currentUserId.Value);

        if (search is not null)
        {
            query = query.Where(x =>
                x.Title.Contains(search) ||
                x.Content.Contains(search) ||
                x.User.UserName != null && x.User.UserName.Contains(search));
        }

        if (categorySlug is not null)
            query = query.Where(x => x.Category.Slug == categorySlug);

        var totalCount = await query.CountAsync(cancellationToken);
        query = ApplySorting(query, filter.SortBy);

        var items = await ProjectListItems(query, currentUserId, canModerate)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return CreatePagedResult(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public async Task<PagedResultDTO<DiscussionListItemDTO>> GetDeletedAsync(
        DiscussionFilterDTO filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ValidatePagination(filter.PageNumber, filter.PageSize);

        var search = filter.Search.NormalizeOptionalText("AxtarÄ±ÅŸ mÉ™tni", MaxSearchLength);
        var categorySlug = filter.CategorySlug
            .NormalizeOptionalText("Kateqoriya slug-u", 120)?
            .ToLowerInvariant();

        var query = _context.Discussions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.IsDeleted);

        if (search is not null)
        {
            query = query.Where(x =>
                x.Title.Contains(search) ||
                x.Content.Contains(search));
        }

        if (categorySlug is not null)
            query = query.Where(x => x.Category.Slug == categorySlug);

        var totalCount = await query.CountAsync(cancellationToken);
        query = ApplySorting(query, filter.SortBy, ignoreReplyFilters: true);

        var items = await ProjectListItems(query, null, true, ignoreReplyFilters: true)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return CreatePagedResult(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public async Task<DiscussionDetailDTO> GetByIdAsync(
        int id,
        Guid? currentUserId,
        bool canModerate,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidId();

        await EnsureCanViewAsync(
            _context.Discussions.Where(x => x.Id == id),
            currentUserId,
            canModerate,
            cancellationToken);

        await RecordUniqueViewAsync(id, currentUserId, cancellationToken);

        return await ProjectDetails(
                _context.Discussions.AsNoTracking().Where(x => x.Id == id),
                currentUserId,
                canModerate)
            .FirstAsync(cancellationToken);
    }

    public async Task<DiscussionDetailDTO> GetBySlugAsync(
        string slug,
        Guid? currentUserId,
        bool canModerate,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = slug.NormalizeRequiredText("MÃ¶vzu slug-u", 240)
            .ToLowerInvariant();

        var discussionId = await _context.Discussions
            .AsNoTracking()
            .Where(x => x.Slug == normalizedSlug)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("MÃ¶vzu tapÄ±lmadÄ±.");

        return await GetByIdAsync(
            discussionId,
            currentUserId,
            canModerate,
            cancellationToken);
    }

    public async Task<DiscussionDetailDTO> CreateAsync(
        Guid userId,
        DiscussionCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        dto.CategoryId.EnsureValidId("Kateqoriya ID-si");

        var userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, cancellationToken);

        if (!userExists)
            throw new NotFoundException("Ä°stifadÉ™Ã§i tapÄ±lmadÄ±.");

        var categoryExists = await _context.ForumCategories
            .AsNoTracking()
            .AnyAsync(x => x.Id == dto.CategoryId, cancellationToken);

        if (!categoryExists)
            throw new NotFoundException("Forum kateqoriyasÄ± tapÄ±lmadÄ±.");

        if (dto.CommunityId.HasValue)
        {
            var canPostInCommunity = await _context.ForumCommunityMembers
                .AsNoTracking()
                .AnyAsync(member =>
                    member.CommunityId == dto.CommunityId.Value &&
                    member.UserId == userId &&
                    member.Status == ForumCommunityMembershipStatus.Active,
                    cancellationToken);

            if (!canPostInCommunity)
            {
                throw new ForbiddenException(
                    "Bu icmada mÃ¼zakirÉ™ aÃ§maq Ã¼Ã§Ã¼n aktiv Ã¼zv olmalÄ±sÄ±nÄ±z.");
            }
        }

        var title = dto.Title.NormalizeRequiredText("MÃ¶vzu baÅŸlÄ±ÄŸÄ±", MaxTitleLength);
        var content = dto.Content.NormalizeOptionalText("MÃ¶vzu mÉ™tni", MaxContentLength)
            ?? string.Empty;

        if (dto.Media.Count > MaxMediaCount)
            throw new BadRequestException(
                $"Bir mÃ¼zakirÉ™yÉ™ maksimum {MaxMediaCount} media É™lavÉ™ edilÉ™ bilÉ™r.");

        if (content.Length == 0 && dto.Media.Count == 0 && dto.Poll is null)
            throw new BadRequestException(
                "MÃ¼zakirÉ™ mÉ™tni vÉ™ ya É™n azÄ± bir media É™lavÉ™ edilmÉ™lidir.");

        var slug = await CreateUniqueSlugAsync(title, cancellationToken);

        var discussion = new Discussion
        {
            Title = title,
            Slug = slug,
            Content = content,
            CategoryId = dto.CategoryId,
            CommunityId = dto.CommunityId,
            IsSpoiler = dto.IsSpoiler,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            Poll = CreatePoll(dto.Poll, DateTime.UtcNow)
        };

        var savedUrls = new List<string>();

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            await _context.Discussions.AddAsync(discussion, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            for (var index = 0; index < dto.Media.Count; index++)
            {
                var upload = dto.Media[index];
                var stored = await _fileStorageService.SaveDiscussionMediaAsync(
                    upload,
                    cancellationToken);
                savedUrls.Add(stored.Url);

                await _context.DiscussionMedia.AddAsync(
                    new DiscussionMedia
                    {
                        DiscussionId = discussion.Id,
                        Url = stored.Url,
                        ContentType = stored.ContentType,
                        OriginalFileName = Path.GetFileName(upload.FileName),
                        FileSize = stored.Length,
                        SortOrder = index,
                        CreatedAt = DateTime.UtcNow
                    },
                    cancellationToken);
            }

            if (!discussion.CommunityId.HasValue)
            {
                await _notificationService.QueueForFollowersAsync(
                    new FollowerNotificationRequest
                    {
                        ActorUserId = userId,
                        Type = NotificationType.FollowingDiscussionCreated,
                        EntityType = NotificationEntityType.Discussion,
                        EntityId = discussion.Id.ToString(),
                        Title = "Yeni mÃ¼zakirÉ™",
                        Message = $"{discussion.Title} mÃ¶vzusunda yeni mÃ¼zakirÉ™ aÃ§dÄ±.",
                        ActionUrl = $"/forum/discussions/{discussion.Slug}"
                    },
                    cancellationToken);
            }
            else
            {
                var community = await _context.ForumCommunities
                    .AsNoTracking()
                    .Where(x => x.Id == discussion.CommunityId.Value)
                    .Select(x => new
                    {
                        x.Name,
                        ActiveMemberIds = x.Members
                            .Where(member =>
                                member.Status == ForumCommunityMembershipStatus.Active &&
                                member.UserId != userId)
                            .Select(member => member.UserId)
                            .ToList(),
                        FollowerIds = x.Followers
                            .Where(follow => follow.UserId != userId)
                            .Select(follow => follow.UserId)
                            .ToList()
                    })
                    .FirstAsync(cancellationToken);

                var communityRecipientIds = community.ActiveMemberIds
                    .Concat(community.FollowerIds)
                    .ToHashSet();

                foreach (var recipientId in communityRecipientIds)
                {
                    await _notificationService.QueueAsync(
                        new NotificationCreateRequest
                        {
                            RecipientUserId = recipientId,
                            ActorUserId = userId,
                            Type = NotificationType.CommunityDiscussionCreated,
                            EntityType = NotificationEntityType.Discussion,
                            EntityId = discussion.Id.ToString(),
                            Title = $"{community.Name} icmasÄ±nda yeni mÃ¼zakirÉ™",
                            Message = discussion.Title,
                            ActionUrl = $"/forum/discussions/{discussion.Slug}"
                        },
                        cancellationToken);
                }

                var actorFollowerIds = await _context.UserFollows
                    .AsNoTracking()
                    .Where(follow =>
                        follow.FollowingId == userId &&
                        follow.Status == FollowStatus.Accepted &&
                        follow.FollowerId != userId)
                    .Select(follow => follow.FollowerId)
                    .ToListAsync(cancellationToken);

                foreach (var followerId in actorFollowerIds
                             .Where(id => !communityRecipientIds.Contains(id)))
                {
                    await _notificationService.QueueAsync(
                        new NotificationCreateRequest
                        {
                            RecipientUserId = followerId,
                            ActorUserId = userId,
                            Type = NotificationType.FollowingDiscussionCreated,
                            EntityType = NotificationEntityType.Discussion,
                            EntityId = discussion.Id.ToString(),
                            Title = "Yeni mÃ¼zakirÉ™",
                            Message = $"{discussion.Title} mÃ¶vzusunda yeni mÃ¼zakirÉ™ aÃ§dÄ±.",
                            ActionUrl = $"/forum/discussions/{discussion.Slug}"
                        },
                        cancellationToken);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            foreach (var url in savedUrls)
                await _fileStorageService.DeleteAsync(url);
            throw;
        }

        return await ProjectDetails(
                _context.Discussions.AsNoTracking().Where(x => x.Id == discussion.Id),
                userId,
                false)
            .FirstAsync(cancellationToken);
    }

    public async Task<DiscussionDetailDTO> UpdateAsync(
        int id,
        Guid userId,
        bool canModerate,
        DiscussionUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidId();
        ArgumentNullException.ThrowIfNull(dto);
        dto.CategoryId.EnsureValidId("Kateqoriya ID-si");

        var discussion = await _context.Discussions
            .Include(x => x.Media)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("MÃ¶vzu tapÄ±lmadÄ±.");

        EnsureCanModify(discussion.UserId, userId, canModerate);

        var categoryExists = await _context.ForumCategories
            .AsNoTracking()
            .AnyAsync(x => x.Id == dto.CategoryId, cancellationToken);

        if (!categoryExists)
            throw new NotFoundException("Forum kateqoriyasÄ± tapÄ±lmadÄ±.");

        var title = dto.Title.NormalizeRequiredText(
            "MÃ¶vzu baÅŸlÄ±ÄŸÄ±",
            MaxTitleLength);
        var content = dto.Content
            .NormalizeOptionalText("MÃ¶vzu mÉ™tni", MaxContentLength)
            ?? string.Empty;

        var existingMedia = discussion.Media
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToList();

        List<DiscussionMedia> keptMedia;

        if (dto.KeptMediaIds is null)
        {
            // KÃ¶hnÉ™ JSON redaktÉ™ sorÄŸularÄ± media gÃ¶ndÉ™rmÉ™diyi Ã¼Ã§Ã¼n
            // mÃ¶vcud media olduÄŸu kimi qorunur.
            keptMedia = existingMedia;
        }
        else
        {
            var requestedIds = dto.KeptMediaIds
                .Distinct()
                .ToList();
            var existingById = existingMedia
                .ToDictionary(x => x.Id);

            if (requestedIds.Any(mediaId => !existingById.ContainsKey(mediaId)))
            {
                throw new BadRequestException(
                    "SaxlanÄ±lmaq Ã¼Ã§Ã¼n seÃ§ilÉ™n media bu mÃ¼zakirÉ™yÉ™ aid deyil.");
            }

            keptMedia = requestedIds
                .Select(mediaId => existingById[mediaId])
                .ToList();
        }

        var newMedia = dto.Media ?? [];
        var resultingMediaCount = keptMedia.Count + newMedia.Count;

        if (resultingMediaCount > MaxMediaCount)
        {
            throw new BadRequestException(
                $"Bir mÃ¼zakirÉ™yÉ™ maksimum {MaxMediaCount} media É™lavÉ™ edilÉ™ bilÉ™r.");
        }

        if (content.Length == 0 && resultingMediaCount == 0)
        {
            throw new BadRequestException(
                "MÃ¼zakirÉ™ mÉ™tni vÉ™ ya É™n azÄ± bir media saxlanmalÄ±dÄ±r.");
        }

        var keptIds = keptMedia
            .Select(x => x.Id)
            .ToHashSet();
        var removedMedia = existingMedia
            .Where(x => !keptIds.Contains(x.Id))
            .ToList();
        var savedUrls = new List<string>();
        var now = DateTime.UtcNow;

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            discussion.Title = title;
            discussion.Content = content;
            discussion.CategoryId = dto.CategoryId;
            discussion.IsSpoiler = dto.IsSpoiler;
            discussion.UpdatedAt = now;

            for (var index = 0; index < keptMedia.Count; index++)
                keptMedia[index].SortOrder = index;

            if (removedMedia.Count > 0)
                _context.DiscussionMedia.RemoveRange(removedMedia);

            for (var index = 0; index < newMedia.Count; index++)
            {
                var upload = newMedia[index];
                var stored = await _fileStorageService
                    .SaveDiscussionMediaAsync(upload, cancellationToken);
                savedUrls.Add(stored.Url);

                await _context.DiscussionMedia.AddAsync(
                    new DiscussionMedia
                    {
                        DiscussionId = discussion.Id,
                        Url = stored.Url,
                        ContentType = stored.ContentType,
                        OriginalFileName = Path.GetFileName(upload.FileName),
                        FileSize = stored.Length,
                        SortOrder = keptMedia.Count + index,
                        CreatedAt = now
                    },
                    cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);

            foreach (var url in savedUrls)
                await _fileStorageService.DeleteAsync(url);

            throw;
        }

        foreach (var media in removedMedia)
            await _fileStorageService.DeleteAsync(media.Url);

        return await ProjectDetails(
                _context.Discussions.AsNoTracking().Where(x => x.Id == discussion.Id),
                userId,
                canModerate)
            .FirstAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int id,
        Guid userId,
        bool canModerate,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidId();

        var discussion = await _context.Discussions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("MÃ¶vzu tapÄ±lmadÄ±.");

        if (discussion.UserId != userId && !canModerate)
        {
            if (!discussion.CommunityId.HasValue)
                throw new ForbiddenException("Bu mÃ¶vzunu silmÉ™k Ã¼Ã§Ã¼n icazÉ™niz yoxdur.");

            var currentRole = await _context.ForumCommunityMembers
                .AsNoTracking()
                .Where(member =>
                    member.CommunityId == discussion.CommunityId.Value &&
                    member.UserId == userId &&
                    member.Status == ForumCommunityMembershipStatus.Active)
                .Select(member => (ForumCommunityMemberRole?)member.Role)
                .FirstOrDefaultAsync(cancellationToken);

            var authorRole = await _context.ForumCommunityMembers
                .AsNoTracking()
                .Where(member =>
                    member.CommunityId == discussion.CommunityId.Value &&
                    member.UserId == discussion.UserId &&
                    member.Status == ForumCommunityMembershipStatus.Active)
                .Select(member => (ForumCommunityMemberRole?)member.Role)
                .FirstOrDefaultAsync(cancellationToken);

            var canCommunityDelete =
                currentRole == ForumCommunityMemberRole.Owner ||
                currentRole == ForumCommunityMemberRole.Moderator &&
                 authorRole is null or ForumCommunityMemberRole.Member;

            if (!canCommunityDelete)
                throw new ForbiddenException("Bu mÃ¶vzunu silmÉ™k Ã¼Ã§Ã¼n icazÉ™niz yoxdur.");
        }

        discussion.IsDeleted = true;
        discussion.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task LikeAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidId();

        await EnsureCanViewAsync(
            _context.Discussions.Where(x => x.Id == id),
            userId,
            false,
            cancellationToken);

        var discussionInfo = await _context.Discussions
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.UserId,
                x.Slug
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("MÃ¶vzu tapÄ±lmadÄ±.");

        await EnsureUsersAreNotBlockedAsync(
            userId,
            discussionInfo.UserId,
            cancellationToken);

        var alreadyLiked = await _context.DiscussionLikes
            .AnyAsync(x => x.DiscussionId == id && x.UserId == userId, cancellationToken);

        if (alreadyLiked)
            throw new ConflictException("Bu mÃ¶vzunu artÄ±q bÉ™yÉ™nmisiniz.");

        await _context.DiscussionLikes.AddAsync(new DiscussionLike
        {
            DiscussionId = id,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _notificationService.QueueAsync(
            new NotificationCreateRequest
            {
                RecipientUserId = discussionInfo.UserId,
                ActorUserId = userId,
                Type = NotificationType.DiscussionLiked,
                Priority = NotificationPriority.Low,
                EntityType = NotificationEntityType.Discussion,
                EntityId = id.ToString(),
                Title = "MÃ¼zakirÉ™niz bÉ™yÉ™nildi",
                Message = "AÃ§dÄ±ÄŸÄ±nÄ±z mÃ¼zakirÉ™ni bÉ™yÉ™ndi.",
                ActionUrl = $"/forum/discussions/{discussionInfo.Slug}"
            },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UnlikeAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidId();

        var like = await _context.DiscussionLikes
            .FirstOrDefaultAsync(
                x => x.DiscussionId == id && x.UserId == userId,
                cancellationToken)
            ?? throw new NotFoundException("MÃ¶vzu bÉ™yÉ™nmÉ™si tapÄ±lmadÄ±.");

        _context.DiscussionLikes.Remove(like);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DiscussionPollDTO> VoteAsync(
        int discussionId,
        Guid userId,
        DiscussionPollVoteRequestDTO dto,
        CancellationToken cancellationToken = default)
    {
        discussionId.EnsureValidId("MÃ¼zakirÉ™ ID-si");
        ArgumentNullException.ThrowIfNull(dto);
        dto.OptionId.EnsureValidId("SeÃ§im ID-si");

        var poll = await _context.DiscussionPolls
            .Include(x => x.Options)
            .Include(x => x.Votes.Where(vote => vote.UserId == userId))
            .FirstOrDefaultAsync(x => x.DiscussionId == discussionId, cancellationToken)
            ?? throw new NotFoundException("Bu mÃ¼zakirÉ™dÉ™ sorÄŸu yoxdur.");

        var nowUtc = DateTime.UtcNow;
        if (poll.IsClosed || poll.EndsAtUtc <= nowUtc)
            throw new ConflictException("Bu sorÄŸu artÄ±q bitib.");

        if (!poll.Options.Any(x => x.Id == dto.OptionId))
            throw new BadRequestException("SeÃ§ilmiÅŸ variant bu sorÄŸuya aid deyil.");

        var existingVote = poll.Votes.SingleOrDefault();
        if (existingVote is null)
        {
            await _context.DiscussionPollVotes.AddAsync(
                new DiscussionPollVote
                {
                    PollId = poll.Id,
                    OptionId = dto.OptionId,
                    UserId = userId,
                    CreatedAt = nowUtc
                },
                cancellationToken);
        }
        else if (existingVote.OptionId != dto.OptionId)
        {
            if (!poll.AllowVoteChange)
                throw new ConflictException("Bu sorÄŸuda verilmiÅŸ sÉ™si dÉ™yiÅŸmÉ™k mÃ¼mkÃ¼n deyil.");

            existingVote.OptionId = dto.OptionId;
            existingVote.UpdatedAt = nowUtc;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return (await ProjectDetails(
                _context.Discussions.AsNoTracking().Where(x => x.Id == discussionId),
                userId,
                false)
            .FirstAsync(cancellationToken)).Poll!;
    }

    public async Task<DiscussionPollDTO> ClosePollAsync(
        int discussionId,
        Guid userId,
        bool canModerate,
        CancellationToken cancellationToken = default)
    {
        discussionId.EnsureValidId("MÃ¼zakirÉ™ ID-si");

        var poll = await _context.DiscussionPolls
            .Include(x => x.Discussion)
            .FirstOrDefaultAsync(x => x.DiscussionId == discussionId, cancellationToken)
            ?? throw new NotFoundException("Bu mÃ¼zakirÉ™dÉ™ sorÄŸu yoxdur.");

        EnsureCanModify(poll.Discussion.UserId, userId, canModerate);

        if (poll.IsClosed)
            throw new ConflictException("Bu sorÄŸu artÄ±q baÄŸlanÄ±b.");

        poll.IsClosed = true;
        poll.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return (await ProjectDetails(
                _context.Discussions.AsNoTracking().Where(x => x.Id == discussionId),
                userId,
                canModerate)
            .FirstAsync(cancellationToken)).Poll!;
    }

    public async Task SaveAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidId();

        await EnsureCanViewAsync(
            _context.Discussions.Where(x => x.Id == id),
            userId,
            false,
            cancellationToken);

        var authorId = await _context.Discussions
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => (Guid?)x.UserId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("MÃ¼zakirÉ™ tapÄ±lmadÄ±.");

        if (authorId == userId)
            throw new ConflictException("Ã–z mÃ¼zakirÉ™nizi yadda saxlamaq olmaz.");

        await EnsureUsersAreNotBlockedAsync(userId, authorId, cancellationToken);

        var alreadySaved = await _context.DiscussionSaves
            .AnyAsync(
                x => x.DiscussionId == id && x.UserId == userId,
                cancellationToken);

        if (alreadySaved)
            return;

        await _context.DiscussionSaves.AddAsync(
            new DiscussionSave
            {
                DiscussionId = id,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UnsaveAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidId();

        var savedDiscussion = await _context.DiscussionSaves
            .FirstOrDefaultAsync(
                x => x.DiscussionId == id && x.UserId == userId,
                cancellationToken);

        if (savedDiscussion is null)
            return;

        _context.DiscussionSaves.Remove(savedDiscussion);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPinnedAsync(
        int id,
        bool isPinned,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidId();

        var discussion = await _context.Discussions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("MÃ¶vzu tapÄ±lmadÄ±.");

        if (discussion.IsPinned == isPinned)
            return;

        discussion.IsPinned = isPinned;
        discussion.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetLockedAsync(
        int id,
        bool isLocked,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidId();

        var discussion = await _context.Discussions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("MÃ¶vzu tapÄ±lmadÄ±.");

        if (discussion.IsLocked == isLocked)
            return;

        discussion.IsLocked = isLocked;
        discussion.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DiscussionDetailDTO> RestoreAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidId();

        var discussion = await _context.Discussions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("MÃ¶vzu tapÄ±lmadÄ±.");

        var categoryIsActive = await _context.ForumCategories
            .AsNoTracking()
            .AnyAsync(x => x.Id == discussion.CategoryId, cancellationToken);

        if (!categoryIsActive)
        {
            throw new ConflictException(
                "MÃ¶vzunun kateqoriyasÄ± silinib. ÆvvÉ™lcÉ™ kateqoriyanÄ± bÉ™rpa edin.");
        }

        if (discussion.IsDeleted)
        {
            discussion.IsDeleted = false;
            discussion.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await ProjectDetails(
                _context.Discussions.AsNoTracking().Where(x => x.Id == id),
                null,
                true)
            .FirstAsync(cancellationToken);
    }

    private IQueryable<Discussion> ExcludeBlockedAuthors(
        IQueryable<Discussion> query,
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

    private static IQueryable<Discussion> ApplySorting(
        IQueryable<Discussion> query,
        string? sortBy,
        bool ignoreReplyFilters = false)
    {
        var normalizedSort = string.IsNullOrWhiteSpace(sortBy)
            ? "newest"
            : sortBy.Trim().ToLowerInvariant();

        var nowUtc = DateTime.UtcNow;

        return normalizedSort switch
        {
            "newest" => query
                .OrderByDescending(x => x.IsPinned)
                .ThenByDescending(x => x.CreatedAt),

            "oldest" => query
                .OrderByDescending(x => x.IsPinned)
                .ThenBy(x => x.CreatedAt),

            "trending" => query
                .OrderByDescending(x => x.IsPinned)
                .ThenByDescending(x =>
                    x.Likes.Count * 12 +
                    (ignoreReplyFilters
                        ? x.Replies.Count(reply => !reply.IsDeleted)
                        : x.Replies.Count) * 18 +
                    x.ViewCount / 25 -
                    EF.Functions.DateDiffHour(
                        x.UpdatedAt ?? x.CreatedAt,
                        nowUtc) / 4)
                .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt),

            "popular" or "most-liked" => query
                .OrderByDescending(x => x.IsPinned)
                .ThenByDescending(x => x.Likes.Count)
                .ThenByDescending(x => x.CreatedAt),

            "most-replied" => query
                .OrderByDescending(x => x.IsPinned)
                .ThenByDescending(x => ignoreReplyFilters
                    ? x.Replies.Count(reply => !reply.IsDeleted)
                    : x.Replies.Count)
                .ThenByDescending(x => x.CreatedAt),

            "most-viewed" => query
                .OrderByDescending(x => x.IsPinned)
                .ThenByDescending(x => x.ViewCount)
                .ThenByDescending(x => x.CreatedAt),

            "recently-updated" => query
                .OrderByDescending(x => x.IsPinned)
                .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt),

            _ => throw new BadRequestException(
                "SortBy yalnÄ±z newest, oldest, trending, popular, most-liked, most-replied, most-viewed vÉ™ recently-updated ola bilÉ™r.")
        };
    }

    private static IQueryable<DiscussionListItemDTO> ProjectListItems(
        IQueryable<Discussion> query,
        Guid? currentUserId,
        bool canModerate,
        bool ignoreReplyFilters = false)
    {
        return query.Select(x => new DiscussionListItemDTO
        {
            Id = x.Id,
            Title = x.Title,
            Slug = x.Slug,
            ContentPreview = x.Content.Length > 280
                ? x.Content.Substring(0, 280)
                : x.Content,
            Media = x.Media
                .OrderBy(media => media.SortOrder)
                .Select(media => new DiscussionMediaDTO
                {
                    Id = media.Id,
                    Url = media.Url,
                    ContentType = media.ContentType,
                    OriginalFileName = media.OriginalFileName,
                    FileSize = media.FileSize,
                    SortOrder = media.SortOrder
                })
                .ToList(),
            Poll = x.Poll == null ? null : new DiscussionPollPreviewDTO
            {
                Id = x.Poll.Id,
                Question = x.Poll.Question,
                EndsAtUtc = x.Poll.EndsAtUtc,
                IsClosed = x.Poll.IsClosed,
                Options = x.Poll.Options
                    .OrderBy(option => option.DisplayOrder)
                    .Select(option => new DiscussionPollPreviewOptionDTO
                    {
                        Id = option.Id,
                        Text = option.Text,
                        DisplayOrder = option.DisplayOrder
                    })
                    .ToList()
            },
            CategoryId = x.CategoryId,
            CategoryName = x.Category.Name,
            CategorySlug = x.Category.Slug,
            CommunityId = x.CommunityId,
            CommunityName = x.Community != null ? x.Community.Name : null,
            CommunitySlug = x.Community != null ? x.Community.Slug : null,
            CommunityIconUrl = x.Community != null ? x.Community.IconUrl : null,
            AuthorId = x.UserId,
            AuthorUserName = x.User.UserName ?? string.Empty,
            AuthorProfileImageUrl = x.User.ProfileImageUrl,
            AuthorCommunityRole = x.CommunityId.HasValue
                ? x.Community!.Members
                    .Where(member =>
                        member.UserId == x.UserId &&
                        member.Status == ForumCommunityMembershipStatus.Active)
                    .Select(member => (ForumCommunityMemberRole?)member.Role)
                    .FirstOrDefault()
                : null,
            AuthorCommunityFlairName = x.CommunityId.HasValue
                ? x.Community!.Members
                    .Where(member => member.UserId == x.UserId && member.Status == ForumCommunityMembershipStatus.Active)
                    .Select(member => member.Flair != null ? member.Flair.Name : null)
                    .FirstOrDefault()
                : null,
            AuthorCommunityFlairBackgroundColor = x.CommunityId.HasValue
                ? x.Community!.Members
                    .Where(member => member.UserId == x.UserId && member.Status == ForumCommunityMembershipStatus.Active)
                    .Select(member => member.Flair != null ? member.Flair.BackgroundColor : null)
                    .FirstOrDefault()
                : null,
            AuthorCommunityFlairTextColor = x.CommunityId.HasValue
                ? x.Community!.Members
                    .Where(member => member.UserId == x.UserId && member.Status == ForumCommunityMembershipStatus.Active)
                    .Select(member => member.Flair != null ? member.Flair.TextColor : null)
                    .FirstOrDefault()
                : null,
            LikeCount = x.Likes.Count,
            ReplyCount = ignoreReplyFilters
                ? x.Replies.Count(reply => !reply.IsDeleted)
                : x.Replies.Count,
            ViewCount = x.ViewCount,
            IsLikedByCurrentUser = currentUserId.HasValue &&
                                   x.Likes.Any(like => like.UserId == currentUserId.Value),
            IsSavedByCurrentUser = currentUserId.HasValue &&
                                   x.UserId != currentUserId.Value &&
                                   x.Saves.Any(saved => saved.UserId == currentUserId.Value),
            IsPinned = x.IsPinned,
            IsLocked = x.IsLocked,
            IsSpoiler = x.IsSpoiler,
            IsDeleted = x.IsDeleted,
            CanEdit = currentUserId.HasValue &&
                      (canModerate || x.UserId == currentUserId.Value),
            CanDelete = currentUserId.HasValue &&
                (canModerate ||
                 x.UserId == currentUserId.Value ||
                 x.CommunityId.HasValue &&
                  (x.Community!.Members.Any(member =>
                       member.UserId == currentUserId.Value &&
                       member.Status == ForumCommunityMembershipStatus.Active &&
                       member.Role == ForumCommunityMemberRole.Owner) ||
                   x.Community!.Members.Any(member =>
                        member.UserId == currentUserId.Value &&
                        member.Status == ForumCommunityMembershipStatus.Active &&
                        member.Role == ForumCommunityMemberRole.Moderator) &&
                    !x.Community!.Members.Any(member =>
                        member.UserId == x.UserId &&
                        member.Status == ForumCommunityMembershipStatus.Active &&
                        (member.Role == ForumCommunityMemberRole.Owner ||
                         member.Role == ForumCommunityMemberRole.Moderator)))),
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        });
    }

    private static IQueryable<DiscussionDetailDTO> ProjectDetails(
        IQueryable<Discussion> query,
        Guid? currentUserId,
        bool canModerate)
    {
        var nowUtc = DateTime.UtcNow;
        return query.Select(x => new DiscussionDetailDTO
        {
            Id = x.Id,
            Title = x.Title,
            Slug = x.Slug,
            Content = x.Content,
            Media = x.Media
                .OrderBy(media => media.SortOrder)
                .Select(media => new DiscussionMediaDTO
                {
                    Id = media.Id,
                    Url = media.Url,
                    ContentType = media.ContentType,
                    OriginalFileName = media.OriginalFileName,
                    FileSize = media.FileSize,
                    SortOrder = media.SortOrder
                })
                .ToList(),
            Poll = x.Poll == null ? null : new DiscussionPollDTO
            {
                Id = x.Poll.Id,
                Question = x.Poll.Question,
                EndsAtUtc = x.Poll.EndsAtUtc,
                IsClosed = x.Poll.IsClosed,
                HasEnded = x.Poll.IsClosed || x.Poll.EndsAtUtc <= nowUtc,
                AllowVoteChange = x.Poll.AllowVoteChange,
                ResultsVisibility = x.Poll.ResultsVisibility,
                HasVoted = currentUserId.HasValue &&
                           x.Poll.Votes.Any(vote => vote.UserId == currentUserId.Value),
                SelectedOptionId = currentUserId.HasValue
                    ? x.Poll.Votes
                        .Where(vote => vote.UserId == currentUserId.Value)
                        .Select(vote => (int?)vote.OptionId)
                        .FirstOrDefault()
                    : null,
                CanViewResults =
                    x.Poll.ResultsVisibility == PollResultsVisibility.Always ||
                    x.Poll.ResultsVisibility == PollResultsVisibility.AfterVote &&
                     currentUserId.HasValue &&
                     x.Poll.Votes.Any(vote => vote.UserId == currentUserId.Value) ||
                    x.Poll.ResultsVisibility == PollResultsVisibility.AfterEnd &&
                     (x.Poll.IsClosed || x.Poll.EndsAtUtc <= nowUtc),
                TotalVotes =
                    x.Poll.ResultsVisibility == PollResultsVisibility.Always ||
                    x.Poll.ResultsVisibility == PollResultsVisibility.AfterVote &&
                     currentUserId.HasValue &&
                     x.Poll.Votes.Any(vote => vote.UserId == currentUserId.Value) ||
                    x.Poll.ResultsVisibility == PollResultsVisibility.AfterEnd &&
                     (x.Poll.IsClosed || x.Poll.EndsAtUtc <= nowUtc)
                        ? x.Poll.Votes.Count
                        : null,
                CanEdit = currentUserId.HasValue &&
                          (canModerate || x.UserId == currentUserId.Value),
                Options = x.Poll.Options
                    .OrderBy(option => option.DisplayOrder)
                    .Select(option => new DiscussionPollOptionDTO
                    {
                        Id = option.Id,
                        Text = option.Text,
                        DisplayOrder = option.DisplayOrder,
                        VoteCount =
                            x.Poll.ResultsVisibility == PollResultsVisibility.Always ||
                            x.Poll.ResultsVisibility == PollResultsVisibility.AfterVote &&
                             currentUserId.HasValue &&
                             x.Poll.Votes.Any(vote => vote.UserId == currentUserId.Value) ||
                            x.Poll.ResultsVisibility == PollResultsVisibility.AfterEnd &&
                             (x.Poll.IsClosed || x.Poll.EndsAtUtc <= nowUtc)
                                ? option.Votes.Count
                                : null,
                        Percentage =
                            (x.Poll.ResultsVisibility == PollResultsVisibility.Always ||
                             x.Poll.ResultsVisibility == PollResultsVisibility.AfterVote &&
                              currentUserId.HasValue &&
                              x.Poll.Votes.Any(vote => vote.UserId == currentUserId.Value) ||
                             x.Poll.ResultsVisibility == PollResultsVisibility.AfterEnd &&
                              (x.Poll.IsClosed || x.Poll.EndsAtUtc <= nowUtc)) &&
                            x.Poll.Votes.Any()
                                ? decimal.Round(
                                    100m * option.Votes.Count / x.Poll.Votes.Count,
                                    1)
                                : null
                    })
                    .ToList(),
                ParticipantPreview = x.Poll.Votes
                    .Where(_ =>
                        x.Poll.ResultsVisibility == PollResultsVisibility.Always ||
                        x.Poll.ResultsVisibility == PollResultsVisibility.AfterVote &&
                         currentUserId.HasValue &&
                         x.Poll.Votes.Any(vote => vote.UserId == currentUserId.Value) ||
                        x.Poll.ResultsVisibility == PollResultsVisibility.AfterEnd &&
                         (x.Poll.IsClosed || x.Poll.EndsAtUtc <= nowUtc))
                    .OrderByDescending(vote => vote.CreatedAt)
                    .Take(5)
                    .Select(vote => new DiscussionPollParticipantDTO
                    {
                        UserId = vote.UserId,
                        UserName = vote.User.UserName ?? string.Empty,
                        AvatarUrl = vote.User.ProfileImageUrl
                    })
                    .ToList()
            },
            CategoryId = x.CategoryId,
            CategoryName = x.Category.Name,
            CategorySlug = x.Category.Slug,
            CommunityId = x.CommunityId,
            CommunityName = x.Community != null ? x.Community.Name : null,
            CommunitySlug = x.Community != null ? x.Community.Slug : null,
            CommunityIconUrl = x.Community != null ? x.Community.IconUrl : null,
            AuthorId = x.UserId,
            AuthorUserName = x.User.UserName ?? string.Empty,
            AuthorProfileImageUrl = x.User.ProfileImageUrl,
            AuthorCommunityRole = x.CommunityId.HasValue
                ? x.Community!.Members
                    .Where(member =>
                        member.UserId == x.UserId &&
                        member.Status == ForumCommunityMembershipStatus.Active)
                    .Select(member => (ForumCommunityMemberRole?)member.Role)
                    .FirstOrDefault()
                : null,
            AuthorCommunityFlairName = x.CommunityId.HasValue
                ? x.Community!.Members
                    .Where(member => member.UserId == x.UserId && member.Status == ForumCommunityMembershipStatus.Active)
                    .Select(member => member.Flair != null ? member.Flair.Name : null)
                    .FirstOrDefault()
                : null,
            AuthorCommunityFlairBackgroundColor = x.CommunityId.HasValue
                ? x.Community!.Members
                    .Where(member => member.UserId == x.UserId && member.Status == ForumCommunityMembershipStatus.Active)
                    .Select(member => member.Flair != null ? member.Flair.BackgroundColor : null)
                    .FirstOrDefault()
                : null,
            AuthorCommunityFlairTextColor = x.CommunityId.HasValue
                ? x.Community!.Members
                    .Where(member => member.UserId == x.UserId && member.Status == ForumCommunityMembershipStatus.Active)
                    .Select(member => member.Flair != null ? member.Flair.TextColor : null)
                    .FirstOrDefault()
                : null,
            LikeCount = x.Likes.Count,
            ReplyCount = x.Replies.Count,
            ViewCount = x.ViewCount,
            IsLikedByCurrentUser = currentUserId.HasValue &&
                                   x.Likes.Any(like => like.UserId == currentUserId.Value),
            IsSavedByCurrentUser = currentUserId.HasValue &&
                                   x.UserId != currentUserId.Value &&
                                   x.Saves.Any(saved => saved.UserId == currentUserId.Value),
            IsPinned = x.IsPinned,
            IsLocked = x.IsLocked,
            IsSpoiler = x.IsSpoiler,
            IsDeleted = x.IsDeleted,
            CanEdit = currentUserId.HasValue &&
                      (canModerate || x.UserId == currentUserId.Value),
            CanDelete = currentUserId.HasValue &&
                (canModerate ||
                 x.UserId == currentUserId.Value ||
                 x.CommunityId.HasValue &&
                  (x.Community!.Members.Any(member =>
                       member.UserId == currentUserId.Value &&
                       member.Status == ForumCommunityMembershipStatus.Active &&
                       member.Role == ForumCommunityMemberRole.Owner) ||
                   x.Community!.Members.Any(member =>
                        member.UserId == currentUserId.Value &&
                        member.Status == ForumCommunityMembershipStatus.Active &&
                        member.Role == ForumCommunityMemberRole.Moderator) &&
                    !x.Community!.Members.Any(member =>
                        member.UserId == x.UserId &&
                        member.Status == ForumCommunityMembershipStatus.Active &&
                        (member.Role == ForumCommunityMemberRole.Owner ||
                         member.Role == ForumCommunityMemberRole.Moderator)))),
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        });
    }

    private async Task EnsureCanViewAsync(
        IQueryable<Discussion> query,
        Guid? currentUserId,
        bool canModerate,
        CancellationToken cancellationToken)
    {
        var access = await query
            .AsNoTracking()
            .Select(x => new
            {
                x.UserId,
                x.CommunityId,
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
            ?? throw new NotFoundException("MÃ¶vzu tapÄ±lmadÄ±.");

        if (access.CommunityVisibility == ForumCommunityVisibility.Private &&
            !canModerate &&
            !access.IsCommunityMember)
        {
            throw new NotFoundException("MÃ¶vzu tapÄ±lmadÄ±.");
        }

        if (currentUserId.HasValue && !canModerate)
        {
            await EnsureUsersAreNotBlockedAsync(
                currentUserId.Value,
                access.UserId,
                cancellationToken);
        }
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
                    x.BlockerId == firstUserId && x.BlockedUserId == secondUserId ||
                    x.BlockerId == secondUserId && x.BlockedUserId == firstUserId,
                cancellationToken);

        if (isBlocked)
            throw new ForbiddenException("BloklanmÄ±ÅŸ istifadÉ™Ã§ilÉ™r arasÄ±nda bu É™mÉ™liyyat mÃ¼mkÃ¼n deyil.");
    }

    private static void EnsureCanModify(
        Guid ownerId,
        Guid currentUserId,
        bool canModerate)
    {
        if (ownerId != currentUserId && !canModerate)
            throw new ForbiddenException("Bu mÃ¶vzunu dÉ™yiÅŸmÉ™k Ã¼Ã§Ã¼n icazÉ™niz yoxdur.");
    }

    private async Task RecordUniqueViewAsync(
        int id,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        if (!currentUserId.HasValue)
            return;

        await using var transaction = await _context.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var authorId = await _context.Discussions
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => (Guid?)x.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!authorId.HasValue || authorId.Value == currentUserId.Value)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var alreadyViewed = await _context.DiscussionViews
            .AnyAsync(
                x => x.DiscussionId == id &&
                     x.UserId == currentUserId.Value,
                cancellationToken);

        if (!alreadyViewed)
        {
            await _context.DiscussionViews.AddAsync(
                new DiscussionView
                {
                    DiscussionId = id,
                    UserId = currentUserId.Value,
                    FirstViewedAt = DateTime.UtcNow
                },
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            await _context.Discussions
                .Where(x => x.Id == id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        x => x.ViewCount,
                        x => x.ViewCount + 1),
                    cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<string> CreateUniqueSlugAsync(
        string title,
        CancellationToken cancellationToken)
    {
        var baseSlug = SlugHelper.Generate(title, "discussion");
        var slug = baseSlug;
        var suffix = 2;

        while (await _context.Discussions
                   .IgnoreQueryFilters()
                   .AsNoTracking()
                   .AnyAsync(x => x.Slug == slug, cancellationToken))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }

    private static DiscussionPoll? CreatePoll(
        DiscussionPollWriteDTO? dto,
        DateTime nowUtc)
    {
        if (dto is null)
            return null;

        var normalized = NormalizePoll(dto, nowUtc);
        return new DiscussionPoll
        {
            Question = normalized.Question,
            EndsAtUtc = normalized.EndsAtUtc,
            ResultsVisibility = normalized.ResultsVisibility,
            AllowVoteChange = normalized.AllowVoteChange,
            CreatedAt = nowUtc,
            Options = normalized.Options
                .Select((text, index) => new DiscussionPollOption
                {
                    Text = text,
                    DisplayOrder = index + 1,
                    CreatedAt = nowUtc
                })
                .ToList()
        };
    }

    private static NormalizedPoll NormalizePoll(
        DiscussionPollWriteDTO dto,
        DateTime nowUtc)
    {
        var question = dto.Question.NormalizeRequiredText(
            "SorÄŸu sualÄ±",
            MaxPollQuestionLength);

        if (dto.Options is null || dto.Options.Count is < 2 or > 6)
            throw new BadRequestException("SorÄŸuda minimum 2, maksimum 6 seÃ§im olmalÄ±dÄ±r.");

        var options = dto.Options
            .Select(option => option.NormalizeRequiredText(
                "SorÄŸu seÃ§imi",
                MaxPollOptionLength))
            .ToList();

        if (options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count)
            throw new BadRequestException("SorÄŸu seÃ§imlÉ™ri tÉ™krarlana bilmÉ™z.");

        if (!Enum.IsDefined(dto.ResultsVisibility))
            throw new BadRequestException("SorÄŸu nÉ™ticÉ™lÉ™rinin gÃ¶rÃ¼nmÉ™ qaydasÄ± etibarsÄ±zdÄ±r.");

        var endsAtUtc = dto.EndsAtUtc.Kind switch
        {
            DateTimeKind.Utc => dto.EndsAtUtc,
            DateTimeKind.Local => dto.EndsAtUtc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dto.EndsAtUtc, DateTimeKind.Utc)
        };

        if (endsAtUtc <= nowUtc)
            throw new BadRequestException("SorÄŸunun bitmÉ™ tarixi gÉ™lÉ™cÉ™kdÉ™ olmalÄ±dÄ±r.");

        if (endsAtUtc > nowUtc.Add(MaxPollDuration))
            throw new BadRequestException("SorÄŸunun mÃ¼ddÉ™ti 30 gÃ¼ndÉ™n Ã§ox ola bilmÉ™z.");

        return new NormalizedPoll(
            question,
            options,
            endsAtUtc,
            dto.ResultsVisibility,
            dto.AllowVoteChange);
    }

    private sealed record NormalizedPoll(
        string Question,
        IReadOnlyList<string> Options,
        DateTime EndsAtUtc,
        PollResultsVisibility ResultsVisibility,
        bool AllowVoteChange);

    private static void ValidatePagination(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            throw new BadRequestException("SÉ™hifÉ™ nÃ¶mrÉ™si 1-dÉ™n kiÃ§ik ola bilmÉ™z.");

        if (pageSize is < 1 or > MaxPageSize)
            throw new BadRequestException($"SÉ™hifÉ™ Ã¶lÃ§Ã¼sÃ¼ 1-{MaxPageSize} aralÄ±ÄŸÄ±nda olmalÄ±dÄ±r.");

        if ((long)(pageNumber - 1) * pageSize > int.MaxValue)
            throw new BadRequestException("SÉ™hifÉ™ parametrlÉ™ri hÉ™ddindÉ™n artÄ±q bÃ¶yÃ¼kdÃ¼r.");
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
