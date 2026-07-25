using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.Forum;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class ForumFeedService : IForumFeedService
{
    private const int MaxPageSize = 50;
    private const int MaxSearchLength = 200;

    private readonly AppDbContext _context;

    public ForumFeedService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ForumFeedResultDTO> GetHomeAsync(
        Guid currentUserId,
        ForumFeedFilterDTO filter,
        bool canModerate,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeFilter(filter);
        var takeCount = GetCandidateTakeCount(normalized.PageNumber, normalized.PageSize);

        var followedUserIdsQuery = _context.UserFollows
            .AsNoTracking()
            .Where(x => x.FollowerId == currentUserId && x.Status == FollowStatus.Accepted)
            .Select(x => x.FollowingId);

        var followedCommunityIdsQuery = _context.ForumCommunityFollows
            .AsNoTracking()
            .Where(x => x.UserId == currentUserId)
            .Select(x => x.CommunityId);

        var memberCommunityIdsQuery = _context.ForumCommunityMembers
            .AsNoTracking()
            .Where(x =>
                x.UserId == currentUserId &&
                x.Status == ForumCommunityMembershipStatus.Active)
            .Select(x => x.CommunityId);

        var followedOrMemberCommunityIdsQuery = followedCommunityIdsQuery
            .Union(memberCommunityIdsQuery);

        var blockedUserIdsQuery = GetBlockedUserIds(currentUserId);

        var postQuery = _context.UserPosts
            .AsNoTracking()
            .Where(x =>
                x.UserId != currentUserId &&
                followedUserIdsQuery.Contains(x.UserId) &&
                !blockedUserIdsQuery.Contains(x.UserId));

        if (normalized.Search is not null)
        {
            postQuery = postQuery.Where(x =>
                x.Content != null && x.Content.Contains(normalized.Search));
        }

        var discussionQuery = _context.Discussions
            .AsNoTracking()
            .Where(x =>
                x.UserId != currentUserId &&
                (followedUserIdsQuery.Contains(x.UserId) ||
                 (x.CommunityId.HasValue &&
                  followedOrMemberCommunityIdsQuery.Contains(x.CommunityId.Value))) &&
                (x.CommunityId == null ||
                 x.Community!.Visibility != ForumCommunityVisibility.Private ||
                 x.Community.Members.Any(member =>
                     member.UserId == currentUserId &&
                     member.Status == ForumCommunityMembershipStatus.Active)));

        if (!canModerate)
            discussionQuery = discussionQuery.Where(x => !blockedUserIdsQuery.Contains(x.UserId));

        discussionQuery = ApplyDiscussionFilters(
            discussionQuery,
            normalized.Search,
            normalized.CategorySlug);

        var postCount = await postQuery.CountAsync(cancellationToken);
        var discussionCount = await discussionQuery.CountAsync(cancellationToken);

        var posts = await ProjectPosts(postQuery, currentUserId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(takeCount)
            .ToListAsync(cancellationToken);

        var discussions = await ProjectDiscussions(
                discussionQuery,
                currentUserId,
                canModerate)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(takeCount)
            .ToListAsync(cancellationToken);

        var followedOrMemberCommunityIds = (await followedOrMemberCommunityIdsQuery
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var items = posts
            .Select(post => new ForumFeedItemDTO
            {
                ItemType = "Post",
                PublishedAt = post.CreatedAt,
                Reason = "İzlədiyin istifadəçi post paylaşdı",
                Post = post
            })
            .Concat(discussions.Select(discussion => new ForumFeedItemDTO
            {
                ItemType = "Discussion",
                PublishedAt = discussion.CreatedAt,
                Reason = discussion.CommunityId.HasValue &&
                         followedOrMemberCommunityIds.Contains(discussion.CommunityId.Value)
                    ? $"İzlədiyin və ya üzv olduğun {discussion.CommunityName} icmasında paylaşıldı"
                    : "İzlədiyin istifadəçi müzakirə başlatdı",
                Discussion = discussion
            }))
            .OrderByDescending(x => x.PublishedAt)
            .ThenByDescending(x => x.Post?.Id ?? (long?)x.Discussion?.Id ?? 0L)
            .Skip((normalized.PageNumber - 1) * normalized.PageSize)
            .Take(normalized.PageSize)
            .ToList();

        return CreateResult(
            items,
            normalized.PageNumber,
            normalized.PageSize,
            postCount + discussionCount);
    }

    public async Task<ForumFeedResultDTO> GetPopularAsync(
        Guid? currentUserId,
        ForumFeedFilterDTO filter,
        bool canModerate,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeFilter(filter);
        var periodStart = ResolvePeriodStart(normalized.Period);

        var query = _context.Discussions
            .AsNoTracking()
            .Where(x =>
                x.CommunityId.HasValue &&
                x.Community!.Visibility != ForumCommunityVisibility.Private);

        if (periodStart.HasValue)
            query = query.Where(x => x.CreatedAt >= periodStart.Value);

        if (currentUserId.HasValue && !canModerate)
        {
            var blockedUserIds = GetBlockedUserIds(currentUserId.Value);
            query = query.Where(x => !blockedUserIds.Contains(x.UserId));
        }

        query = ApplyDiscussionFilters(
            query,
            normalized.Search,
            normalized.CategorySlug);

        var totalCount = await query.CountAsync(cancellationToken);

        var discussions = await ProjectDiscussions(
                query
                    .OrderByDescending(x => x.Likes.Count)
                    .ThenByDescending(x => x.Replies.Count)
                    .ThenByDescending(x => x.ViewCount)
                    .ThenByDescending(x => x.CreatedAt),
                currentUserId,
                canModerate)
            .Skip((normalized.PageNumber - 1) * normalized.PageSize)
            .Take(normalized.PageSize)
            .ToListAsync(cancellationToken);

        var periodLabel = normalized.Period switch
        {
            "day" => "Son 24 saatda icmalarda yüksəlir",
            "month" => "Son 30 gündə icmalarda populyardır",
            "all" => "İcmaların bütün zamanlar üzrə ən populyar mövzularındandır",
            _ => "Son 7 gündə icmalarda trenddədir"
        };

        var items = discussions
            .Select(discussion => new ForumFeedItemDTO
            {
                ItemType = "Discussion",
                PublishedAt = discussion.CreatedAt,
                Reason = periodLabel,
                Discussion = discussion
            })
            .ToList();

        return CreateResult(
            items,
            normalized.PageNumber,
            normalized.PageSize,
            totalCount);
    }

    public async Task<ForumFeedResultDTO> GetExploreAsync(
        Guid? currentUserId,
        ForumFeedFilterDTO filter,
        bool canModerate,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeFilter(filter);
        var takeCount = GetCandidateTakeCount(
            normalized.PageNumber,
            normalized.PageSize);
        var now = DateTime.UtcNow;
        var recentStart = now.AddDays(-90);

        IQueryable<Guid> blockedUserIds = Array.Empty<Guid>().AsQueryable();
        IQueryable<Guid> followedUserIds = Array.Empty<Guid>().AsQueryable();
        IQueryable<int> followedCommunityIds = Array.Empty<int>().AsQueryable();
        IReadOnlyList<int> preferredCategoryIds = [];
        IReadOnlyList<long> localGameIds = [];
        IReadOnlyList<long> igdbGameIds = [];

        if (currentUserId.HasValue)
        {
            blockedUserIds = GetBlockedUserIds(currentUserId.Value);
            followedUserIds = _context.UserFollows
                .AsNoTracking()
                .Where(x =>
                    x.FollowerId == currentUserId.Value &&
                    x.Status == FollowStatus.Accepted)
                .Select(x => x.FollowingId);
            followedCommunityIds = _context.ForumCommunityFollows
                .AsNoTracking()
                .Where(x => x.UserId == currentUserId.Value)
                .Select(x => x.CommunityId);

            preferredCategoryIds = await GetPreferredCategoryIdsAsync(
                currentUserId.Value,
                cancellationToken);

            localGameIds = await _context.UserGames
                .AsNoTracking()
                .Where(x =>
                    x.UserId == currentUserId.Value &&
                    x.Source == CatalogSource.Local)
                .Select(x => x.GameId)
                .Distinct()
                .ToListAsync(cancellationToken);

            igdbGameIds = await _context.UserGames
                .AsNoTracking()
                .Where(x =>
                    x.UserId == currentUserId.Value &&
                    x.Source == CatalogSource.Igdb)
                .Select(x => x.GameId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        var postQuery = _context.UserPosts
            .AsNoTracking()
            .Where(x =>
                !x.User.IsPrivate &&
                x.CreatedAt >= recentStart);

        var discussionQuery = _context.Discussions
            .AsNoTracking()
            .Where(x =>
                x.CreatedAt >= recentStart &&
                (x.CommunityId == null ||
                 x.Community!.Visibility != ForumCommunityVisibility.Private));

        if (currentUserId.HasValue)
        {
            var userId = currentUserId.Value;
            postQuery = postQuery.Where(x =>
                x.UserId != userId &&
                !followedUserIds.Contains(x.UserId) &&
                !blockedUserIds.Contains(x.UserId));

            discussionQuery = discussionQuery.Where(x =>
                x.UserId != userId &&
                !followedUserIds.Contains(x.UserId) &&
                !blockedUserIds.Contains(x.UserId) &&
                (!x.CommunityId.HasValue ||
                 !followedCommunityIds.Contains(x.CommunityId.Value)));
        }

        if (normalized.Search is not null)
        {
            postQuery = postQuery.Where(x =>
                x.Content != null && x.Content.Contains(normalized.Search));
        }

        discussionQuery = ApplyDiscussionFilters(
            discussionQuery,
            normalized.Search,
            normalized.CategorySlug);

        var postCount = await postQuery.CountAsync(cancellationToken);
        var discussionCount = await discussionQuery.CountAsync(cancellationToken);

        var postCandidates = await postQuery
            .Select(x => new ExplorePostCandidate
            {
                Post = new PostDTO
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    UserName = x.User.UserName ?? string.Empty,
                    ProfileImageUrl = x.User.ProfileImageUrl,
                    Content = x.Content,
                    ImageUrl = x.ImageUrl,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    IsMine = currentUserId.HasValue && x.UserId == currentUserId.Value,
                    LikeCount = x.Likes.Count,
                    CommentCount = x.Comments.Count,
                    IsLikedByCurrentUser = currentUserId.HasValue &&
                        x.Likes.Any(like => like.UserId == currentUserId.Value)
                },
                SharedGameCount = currentUserId.HasValue
                    ? x.User.UserGames.Count(game =>
                        (game.Source == CatalogSource.Local && localGameIds.Contains(game.GameId)) ||
                        (game.Source == CatalogSource.Igdb && igdbGameIds.Contains(game.GameId)))
                    : 0,
                LikeCount = x.Likes.Count,
                CommentCount = x.Comments.Count,
                CreatedAt = x.CreatedAt
            })
            .OrderByDescending(x => x.SharedGameCount)
            .ThenByDescending(x => x.LikeCount)
            .ThenByDescending(x => x.CommentCount)
            .ThenByDescending(x => x.CreatedAt)
            .Take(takeCount)
            .ToListAsync(cancellationToken);

        var discussionCandidates = await discussionQuery
            .Select(x => new ExploreDiscussionCandidate
            {
                Discussion = new DiscussionListItemDTO
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
                    LikeCount = x.Likes.Count,
                    ReplyCount = x.Replies.Count,
                    ViewCount = x.ViewCount,
                    IsLikedByCurrentUser = currentUserId.HasValue &&
                        x.Likes.Any(like => like.UserId == currentUserId.Value),
                    IsSavedByCurrentUser = currentUserId.HasValue &&
                        x.Saves.Any(save => save.UserId == currentUserId.Value),
                    IsPinned = x.IsPinned,
                    IsLocked = x.IsLocked,
                    IsDeleted = x.IsDeleted,
                    CanEdit = currentUserId.HasValue &&
                        (canModerate || x.UserId == currentUserId.Value),
                    CanDelete = currentUserId.HasValue &&
                        (canModerate || x.UserId == currentUserId.Value),
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                },
                MatchesPreferredCategory = preferredCategoryIds.Contains(x.CategoryId),
                LikeCount = x.Likes.Count,
                ReplyCount = x.Replies.Count,
                ViewCount = x.ViewCount,
                CreatedAt = x.CreatedAt
            })
            .OrderByDescending(x => x.MatchesPreferredCategory)
            .ThenByDescending(x => x.LikeCount)
            .ThenByDescending(x => x.ReplyCount)
            .ThenByDescending(x => x.ViewCount)
            .ThenByDescending(x => x.CreatedAt)
            .Take(takeCount)
            .ToListAsync(cancellationToken);

        var scoredItems = postCandidates
            .Select(candidate =>
            {
                var gameInterestMatches = CountTextMatches(
                    [candidate.Post.Content],
                    normalized.InterestGameTitles);
                var genreInterestMatches = CountTextMatches(
                    [candidate.Post.Content],
                    normalized.InterestGenres);
                var preferenceMatches = CountTextMatches(
                    [candidate.Post.Content],
                    normalized.InterestPlayStyles.Concat(normalized.InterestPlatforms));

                var reason = gameInterestMatches > 0
                    ? "Seçdiyin oyunlardan biri ilə əlaqəlidir"
                    : genreInterestMatches > 0
                        ? "Seçdiyin janrlardan birinə uyğundur"
                        : candidate.SharedGameCount > 0
                            ? $"Bu istifadəçi ilə {candidate.SharedGameCount} ortaq oyununuz var"
                            : "İzləmədiyin yeni bir yaradıcıdan paylaşım";

                return new ScoredFeedItem
                {
                    Score = gameInterestMatches * 40d +
                            genreInterestMatches * 18d +
                            preferenceMatches * 8d +
                            candidate.SharedGameCount * 30d +
                            candidate.Post.LikeCount * 4d +
                            candidate.Post.CommentCount * 5d +
                            FreshnessScore(candidate.Post.CreatedAt, now),
                    Item = new ForumFeedItemDTO
                    {
                        ItemType = "Post",
                        PublishedAt = candidate.Post.CreatedAt,
                        Reason = reason,
                        Post = candidate.Post
                    }
                };
            })
            .Concat(discussionCandidates.Select(candidate =>
            {
                var searchableText = new string?[]
                {
                    candidate.Discussion.Title,
                    candidate.Discussion.ContentPreview,
                    candidate.Discussion.CategoryName,
                    candidate.Discussion.CategorySlug,
                    candidate.Discussion.CommunityName
                };
                var gameInterestMatches = CountTextMatches(
                    searchableText,
                    normalized.InterestGameTitles);
                var genreInterestMatches = CountTextMatches(
                    searchableText,
                    normalized.InterestGenres);
                var preferenceMatches = CountTextMatches(
                    searchableText,
                    normalized.InterestPlayStyles.Concat(normalized.InterestPlatforms));

                var reason = gameInterestMatches > 0
                    ? "Seçdiyin oyunlardan biri haqqında müzakirədir"
                    : genreInterestMatches > 0
                        ? "Seçdiyin janrlardan birinə uyğundur"
                        : candidate.MatchesPreferredCategory
                            ? "Maraq göstərdiyin forum kateqoriyasına uyğundur"
                            : candidate.Discussion.CommunityName is not null
                                ? $"Kəşf edə biləcəyin {candidate.Discussion.CommunityName} icmasından"
                                : "İzləmədiyin yeni bir müzakirə";

                return new ScoredFeedItem
                {
                    Score = gameInterestMatches * 40d +
                            genreInterestMatches * 18d +
                            preferenceMatches * 8d +
                            (candidate.MatchesPreferredCategory ? 35d : 0d) +
                            candidate.Discussion.LikeCount * 4d +
                            candidate.Discussion.ReplyCount * 5d +
                            candidate.Discussion.ViewCount / 15d +
                            FreshnessScore(candidate.Discussion.CreatedAt, now),
                    Item = new ForumFeedItemDTO
                    {
                        ItemType = "Discussion",
                        PublishedAt = candidate.Discussion.CreatedAt,
                        Reason = reason,
                        Discussion = candidate.Discussion
                    }
                };
            }))
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Item.PublishedAt)
            .Skip((normalized.PageNumber - 1) * normalized.PageSize)
            .Take(normalized.PageSize)
            .Select(x => x.Item)
            .ToList();

        IReadOnlyList<ForumCommunitySummaryDTO> suggestedCommunities = [];
        IReadOnlyList<ForumUserSuggestionDTO> suggestedUsers = [];

        if (normalized.PageNumber == 1)
        {
            suggestedCommunities = await GetSuggestedCommunitiesAsync(
                currentUserId,
                cancellationToken);
            suggestedUsers = await GetSuggestedUsersAsync(
                currentUserId,
                localGameIds,
                igdbGameIds,
                cancellationToken);
        }

        return CreateResult(
            scoredItems,
            normalized.PageNumber,
            normalized.PageSize,
            postCount + discussionCount,
            suggestedCommunities,
            suggestedUsers);
    }

    private IQueryable<Guid> GetBlockedUserIds(Guid currentUserId)
    {
        return _context.UserBlocks
            .AsNoTracking()
            .Where(x => x.BlockerId == currentUserId)
            .Select(x => x.BlockedUserId)
            .Concat(_context.UserBlocks
                .AsNoTracking()
                .Where(x => x.BlockedUserId == currentUserId)
                .Select(x => x.BlockerId));
    }

    private async Task<IReadOnlyList<int>> GetPreferredCategoryIdsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await _context.Discussions
            .AsNoTracking()
            .Where(discussion =>
                discussion.UserId == currentUserId ||
                discussion.Likes.Any(like => like.UserId == currentUserId) ||
                discussion.Saves.Any(save => save.UserId == currentUserId) ||
                discussion.Replies.Any(reply => reply.UserId == currentUserId))
            .GroupBy(discussion => discussion.CategoryId)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .Take(8)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ForumCommunitySummaryDTO>> GetSuggestedCommunitiesAsync(
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var query = _context.ForumCommunities
            .AsNoTracking()
            .Where(x => x.Visibility != ForumCommunityVisibility.Private);

        if (currentUserId.HasValue)
        {
            var userId = currentUserId.Value;
            query = query.Where(x =>
                !x.Followers.Any(follower => follower.UserId == userId) &&
                !x.Members.Any(member =>
                    member.UserId == userId &&
                    member.Status == ForumCommunityMembershipStatus.Active));
        }

        return await query
            .OrderByDescending(x => x.Discussions.Count(discussion =>
                discussion.CreatedAt >= DateTime.UtcNow.AddDays(-30)))
            .ThenByDescending(x => x.Followers.Count)
            .ThenByDescending(x => x.Members.Count(member =>
                member.Status == ForumCommunityMembershipStatus.Active))
            .Select(x => new ForumCommunitySummaryDTO
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                Description = x.Description,
                IconUrl = x.IconUrl,
                CoverImageUrl = x.CoverImageUrl,
                Category = x.Category,
                Visibility = x.Visibility,
                OwnerId = x.OwnerId,
                OwnerUserName = x.Owner.UserName ?? string.Empty,
                MemberCount = x.Members.Count(member =>
                    member.Status == ForumCommunityMembershipStatus.Active),
                FollowerCount = x.Followers.Count,
                DiscussionCount = x.Discussions.Count,
                CurrentUserMembershipStatus = currentUserId.HasValue
                    ? x.Members
                        .Where(member => member.UserId == currentUserId.Value)
                        .Select(member => (ForumCommunityMembershipStatus?)member.Status)
                        .FirstOrDefault()
                    : null,
                IsFollowedByCurrentUser = currentUserId.HasValue &&
                    x.Followers.Any(follower => follower.UserId == currentUserId.Value),
                CanManage = currentUserId.HasValue && x.OwnerId == currentUserId.Value,
                CreatedAt = x.CreatedAt
            })
            .Take(6)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ForumUserSuggestionDTO>> GetSuggestedUsersAsync(
        Guid? currentUserId,
        IReadOnlyList<long> localGameIds,
        IReadOnlyList<long> igdbGameIds,
        CancellationToken cancellationToken)
    {
        var query = _context.Users
            .AsNoTracking()
            .Where(x => !x.IsPrivate && (x.Posts.Any() || x.Discussions.Any()));

        if (currentUserId.HasValue)
        {
            var userId = currentUserId.Value;
            var followedUserIds = _context.UserFollows
                .AsNoTracking()
                .Where(x => x.FollowerId == userId && x.Status == FollowStatus.Accepted)
                .Select(x => x.FollowingId);
            var blockedUserIds = GetBlockedUserIds(userId);

            query = query.Where(x =>
                x.Id != userId &&
                !followedUserIds.Contains(x.Id) &&
                !blockedUserIds.Contains(x.Id));
        }

        var candidates = await query
            .Select(x => new ForumUserSuggestionDTO
            {
                UserId = x.Id,
                UserName = x.UserName ?? string.Empty,
                ProfileImageUrl = x.ProfileImageUrl,
                SharedGameCount = currentUserId.HasValue
                    ? x.UserGames.Count(game =>
                        (game.Source == CatalogSource.Local && localGameIds.Contains(game.GameId)) ||
                        (game.Source == CatalogSource.Igdb && igdbGameIds.Contains(game.GameId)))
                    : 0,
                PostCount = x.Posts.Count,
                DiscussionCount = x.Discussions.Count,
                FollowerCount = x.Followers.Count(follow => follow.Status == FollowStatus.Accepted)
            })
            .OrderByDescending(x => x.SharedGameCount)
            .ThenByDescending(x => x.FollowerCount)
            .ThenByDescending(x => x.PostCount + x.DiscussionCount)
            .Take(6)
            .ToListAsync(cancellationToken);

        return candidates
            .Select(x => x with
            {
                Reason = x.SharedGameCount > 0
                    ? $"{x.SharedGameCount} ortaq oyununuz var"
                    : "Forumda aktiv istifadəçi"
            })
            .ToList();
    }

    private static IQueryable<Discussion> ApplyDiscussionFilters(
        IQueryable<Discussion> query,
        string? search,
        string? categorySlug)
    {
        if (search is not null)
        {
            query = query.Where(x =>
                x.Title.Contains(search) ||
                x.Content.Contains(search));
        }

        if (categorySlug is not null)
            query = query.Where(x => x.Category.Slug == categorySlug);

        return query;
    }

    private static IQueryable<PostDTO> ProjectPosts(
        IQueryable<UserPost> query,
        Guid? currentUserId)
    {
        return query.Select(x => new PostDTO
        {
            Id = x.Id,
            UserId = x.UserId,
            UserName = x.User.UserName ?? string.Empty,
            ProfileImageUrl = x.User.ProfileImageUrl,
            Content = x.Content,
            ImageUrl = x.ImageUrl,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
            IsMine = currentUserId.HasValue && x.UserId == currentUserId.Value,
            LikeCount = x.Likes.Count,
            CommentCount = x.Comments.Count,
            IsLikedByCurrentUser = currentUserId.HasValue &&
                x.Likes.Any(like => like.UserId == currentUserId.Value)
        });
    }

    private static IQueryable<DiscussionListItemDTO> ProjectDiscussions(
        IQueryable<Discussion> query,
        Guid? currentUserId,
        bool canModerate)
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
            LikeCount = x.Likes.Count,
            ReplyCount = x.Replies.Count,
            ViewCount = x.ViewCount,
            IsLikedByCurrentUser = currentUserId.HasValue &&
                x.Likes.Any(like => like.UserId == currentUserId.Value),
            IsSavedByCurrentUser = currentUserId.HasValue &&
                x.Saves.Any(save => save.UserId == currentUserId.Value),
            IsPinned = x.IsPinned,
            IsLocked = x.IsLocked,
            IsDeleted = x.IsDeleted,
            CanEdit = currentUserId.HasValue &&
                (canModerate || x.UserId == currentUserId.Value),
            CanDelete = currentUserId.HasValue &&
                (canModerate || x.UserId == currentUserId.Value),
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        });
    }

    private static NormalizedForumFeedFilter NormalizeFilter(
        ForumFeedFilterDTO filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.PageNumber < 1)
            throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");

        if (filter.PageSize is < 1 or > MaxPageSize)
            throw new BadRequestException($"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");

        if ((long)(filter.PageNumber - 1) * filter.PageSize > int.MaxValue)
            throw new BadRequestException("Səhifə parametrləri həddindən artıq böyükdür.");

        var period = string.IsNullOrWhiteSpace(filter.Period)
            ? "week"
            : filter.Period.Trim().ToLowerInvariant();

        if (period is not ("day" or "week" or "month" or "all"))
            throw new BadRequestException("Period yalnız day, week, month və ya all ola bilər.");

        return new NormalizedForumFeedFilter
        {
            Search = filter.Search.NormalizeOptionalText("Axtarış mətni", MaxSearchLength),
            CategorySlug = filter.CategorySlug
                .NormalizeOptionalText("Kateqoriya slug-u", 120)?
                .ToLowerInvariant(),
            Period = period,
            InterestGameTitles = NormalizeInterestValues(
                filter.InterestGameTitles,
                "Maraq oyunları",
                20),
            InterestGenres = NormalizeInterestValues(
                filter.InterestGenres,
                "Maraq janrları",
                20),
            InterestPlayStyles = NormalizeInterestValues(
                filter.InterestPlayStyles,
                "Oyun tərzləri",
                12),
            InterestPlatforms = NormalizeInterestValues(
                filter.InterestPlatform,
                "Platforma",
                3),
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize
        };
    }

    private static IReadOnlyList<string> NormalizeInterestValues(
        string? value,
        string fieldName,
        int maxItems)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var items = value
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (items.Count > maxItems)
            throw new BadRequestException($"{fieldName} üçün maksimum {maxItems} seçim göndərilə bilər.");

        if (items.Any(item => item.Length > 120))
            throw new BadRequestException($"{fieldName} daxilində seçim 120 simvoldan uzun ola bilməz.");

        return items;
    }

    private static int CountTextMatches(
        IEnumerable<string?> values,
        IEnumerable<string> terms)
    {
        var searchableValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();

        if (searchableValues.Count == 0)
            return 0;

        return terms.Count(term => searchableValues.Any(value =>
            value.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    private static DateTime? ResolvePeriodStart(string period)
    {
        var now = DateTime.UtcNow;

        return period switch
        {
            "day" => now.AddDays(-1),
            "week" => now.AddDays(-7),
            "month" => now.AddDays(-30),
            "all" => null,
            _ => now.AddDays(-7)
        };
    }

    private static int GetCandidateTakeCount(int pageNumber, int pageSize)
    {
        var requiredCount = (long)(pageNumber - 1) * pageSize + pageSize;

        if (requiredCount > int.MaxValue)
            throw new BadRequestException("Səhifə parametrləri həddindən artıq böyükdür.");

        return (int)requiredCount;
    }

    private static double FreshnessScore(DateTime createdAt, DateTime now)
    {
        var ageHours = Math.Max(0d, (now - createdAt).TotalHours);
        return Math.Max(0d, 30d - ageHours / 12d);
    }

    private static ForumFeedResultDTO CreateResult(
        IReadOnlyList<ForumFeedItemDTO> items,
        int pageNumber,
        int pageSize,
        int totalCount,
        IReadOnlyList<ForumCommunitySummaryDTO>? suggestedCommunities = null,
        IReadOnlyList<ForumUserSuggestionDTO>? suggestedUsers = null)
    {
        return new ForumFeedResultDTO
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pageSize),
            SuggestedCommunities = suggestedCommunities ?? [],
            SuggestedUsers = suggestedUsers ?? []
        };
    }

    private sealed record NormalizedForumFeedFilter
    {
        public string? Search { get; init; }
        public string? CategorySlug { get; init; }
        public string Period { get; init; } = "week";
        public IReadOnlyList<string> InterestGameTitles { get; init; } = [];
        public IReadOnlyList<string> InterestGenres { get; init; } = [];
        public IReadOnlyList<string> InterestPlayStyles { get; init; } = [];
        public IReadOnlyList<string> InterestPlatforms { get; init; } = [];
        public int PageNumber { get; init; }
        public int PageSize { get; init; }
    }


    private sealed class ExplorePostCandidate
    {
        public PostDTO Post { get; init; } = null!;
        public int SharedGameCount { get; init; }
        public int LikeCount { get; init; }
        public int CommentCount { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class ExploreDiscussionCandidate
    {
        public DiscussionListItemDTO Discussion { get; init; } = null!;
        public bool MatchesPreferredCategory { get; init; }
        public int LikeCount { get; init; }
        public int ReplyCount { get; init; }
        public int ViewCount { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class ScoredFeedItem
    {
        public double Score { get; init; }
        public ForumFeedItemDTO Item { get; init; } = null!;
    }
}
