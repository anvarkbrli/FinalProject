using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class UserActivityService : IUserActivityService
{
    private const int MaxPageSize = 50;
    private readonly AppDbContext _context;
    private readonly IIgdbClient _igdbClient;

    public UserActivityService(
        AppDbContext context,
        IIgdbClient igdbClient)
    {
        _context = context;
        _igdbClient = igdbClient;
    }

    public async Task<PagedResultDTO<UserActivityDTO>> GetFeedAsync(
        Guid userId,
        UserActivityFilterDTO filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ValidateFilter(filter);

        var userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, cancellationToken);

        if (!userExists)
            throw new NotFoundException("Ä°stifadÉ™Ã§i tapÄ±lmadÄ±.");

        var followedUserIds = _context.UserFollows
            .AsNoTracking()
            .Where(x =>
                x.FollowerId == userId &&
                x.Status == FollowStatus.Accepted)
            .Select(x => x.FollowingId);

        IQueryable<UserActivity> query = _context.UserActivities
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId ||
                followedUserIds.Contains(x.UserId) &&
                x.Type != UserActivityType.UserBlocked &&
                x.Type != UserActivityType.UserUnblocked &&
                x.Type != UserActivityType.UserFollowRequested &&
                x.Type != UserActivityType.FollowRequestCancelled);

        query = filter.Scope switch
        {
            UserActivityFeedScope.Mine => query.Where(x => x.UserId == userId),
            UserActivityFeedScope.Following => query.Where(x => x.UserId != userId),
            _ => query
        };

        query = ApplyCategoryFilter(query, filter.Category)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var activities = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var items = await MapManyAsync(activities, cancellationToken);

        return new PagedResultDTO<UserActivityDTO>
        {
            Items = items,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)filter.PageSize)
        };
    }

    private static IQueryable<UserActivity> ApplyCategoryFilter(
        IQueryable<UserActivity> query,
        UserActivityFeedCategory category)
    {
        return category switch
        {
            UserActivityFeedCategory.Statuses => query.Where(x =>
                x.Type == UserActivityType.GameStatusChanged ||
                x.Type == UserActivityType.GameStatusRemoved),

            UserActivityFeedCategory.Lists => query.Where(x =>
                x.Type == UserActivityType.GameListCreated ||
                x.Type == UserActivityType.GameListUpdated ||
                x.Type == UserActivityType.GameListDeleted ||
                x.Type == UserActivityType.GameAddedToList ||
                x.Type == UserActivityType.GameRemovedFromList ||
                x.Type == UserActivityType.GameListGameReordered ||
                x.Type == UserActivityType.GameListLiked ||
                x.Type == UserActivityType.GameListUnliked ||
                x.Type == UserActivityType.GameListCommented ||
                x.Type == UserActivityType.GameListCommentUpdated ||
                x.Type == UserActivityType.GameListCommentDeleted),

            UserActivityFeedCategory.Ratings => query.Where(x =>
                x.Type == UserActivityType.GameRated ||
                x.Type == UserActivityType.GameRatingRemoved),

            UserActivityFeedCategory.Reviews => query.Where(x =>
                x.Type == UserActivityType.ReviewCreated ||
                x.Type == UserActivityType.ReviewUpdated ||
                x.Type == UserActivityType.ReviewDeleted ||
                x.Type == UserActivityType.ReviewLiked ||
                x.Type == UserActivityType.ReviewUnliked ||
                x.Type == UserActivityType.ReviewCommented ||
                x.Type == UserActivityType.ReviewCommentUpdated ||
                x.Type == UserActivityType.ReviewCommentDeleted),

            UserActivityFeedCategory.Posts => query.Where(x =>
                x.Type == UserActivityType.PostCreated ||
                x.Type == UserActivityType.PostUpdated ||
                x.Type == UserActivityType.PostDeleted ||
                x.Type == UserActivityType.PostLiked ||
                x.Type == UserActivityType.PostUnliked ||
                x.Type == UserActivityType.PostCommented ||
                x.Type == UserActivityType.PostCommentUpdated ||
                x.Type == UserActivityType.PostCommentDeleted),

            _ => query
        };
    }

    private async Task<IReadOnlyList<UserActivityDTO>> MapManyAsync(
        IReadOnlyList<UserActivity> activities,
        CancellationToken cancellationToken)
    {
        if (activities.Count == 0)
            return [];

        var relatedUserIds = activities
            .Select(x => x.UserId)
            .Concat(activities
                .Where(x => x.TargetUserId.HasValue)
                .Select(x => x.TargetUserId!.Value))
            .Distinct()
            .ToList();

        var users = await _context.Users
            .AsNoTracking()
            .Where(x => relatedUserIds.Contains(x.Id))
            .Select(x => new ActivityUserDetails(
                x.Id,
                x.UserName ?? string.Empty,
                x.ProfileImageUrl))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var localGameIds = activities
            .Where(x =>
                x.GameSource == CatalogSource.Local &&
                x.GameId.HasValue &&
                x.GameId.Value <= int.MaxValue)
            .Select(x => (int)x.GameId!.Value)
            .Distinct()
            .ToList();

        var localGames = await _context.Games
            .AsNoTracking()
            .Where(x => localGameIds.Contains(x.Id))
            .Select(x => new ActivityGameDetails(
                x.Id,
                x.Name,
                x.Slug,
                x.CoverImageUrl))
            .ToDictionaryAsync(x => x.GameId, cancellationToken);

        var igdbGameIds = activities
            .Where(x =>
                x.GameSource == CatalogSource.Igdb &&
                x.GameId.HasValue)
            .Select(x => x.GameId!.Value)
            .Distinct()
            .ToList();

        var igdbGames = igdbGameIds.Count == 0
            ? new Dictionary<long, ActivityGameDetails>()
            : (await _igdbClient.GetGamesByIdsAsync(
                    igdbGameIds,
                    cancellationToken))
                .ToDictionary(
                    game => game.Id,
                    game => new ActivityGameDetails(
                        game.Id,
                        game.Name,
                        null,
                        game.CoverImageUrl));

        return activities.Select(activity =>
        {
            users.TryGetValue(activity.UserId, out var actorUser);
            users.TryGetValue(
                activity.TargetUserId ?? Guid.Empty,
                out var targetUser);

            ActivityGameDetails? game = null;

            if (activity.GameSource == CatalogSource.Local &&
                activity.GameId.HasValue &&
                activity.GameId.Value <= int.MaxValue)
            {
                localGames.TryGetValue(activity.GameId.Value, out game);
            }
            else if (activity.GameSource == CatalogSource.Igdb &&
                     activity.GameId.HasValue)
            {
                igdbGames.TryGetValue(activity.GameId.Value, out game);
            }

            return new UserActivityDTO
            {
                Id = activity.Id,
                Type = activity.Type,
                CreatedAt = activity.CreatedAt,
                ActorUserId = activity.UserId,
                ActorUserName = actorUser?.UserName ?? "Ä°stifadÉ™Ã§i",
                ActorUserProfileImageUrl = actorUser?.ProfileImageUrl,
                TargetUserId = activity.TargetUserId,
                TargetUserName = targetUser?.UserName,
                TargetUserProfileImageUrl = targetUser?.ProfileImageUrl,
                GameSource = activity.GameSource,
                GameId = activity.GameId,
                GameName = game?.Name,
                GameSlug = game?.Slug,
                GameCoverImageUrl = game?.CoverImageUrl,
                ReviewId = activity.ReviewId,
                Status = activity.Status,
                Rating = activity.Rating,
                PostId = activity.PostId,
                GameListId = activity.GameListId,
                SubjectTitle = activity.SubjectTitle
            };
        }).ToList();
    }

    private static void ValidateFilter(UserActivityFilterDTO filter)
    {
        if (filter.PageNumber < 1)
            throw new BadRequestException("SÉ™hifÉ™ nÃ¶mrÉ™si 1-dÉ™n kiÃ§ik ola bilmÉ™z.");

        if (filter.PageSize < 1 || filter.PageSize > MaxPageSize)
        {
            throw new BadRequestException(
                $"SÉ™hifÉ™ Ã¶lÃ§Ã¼sÃ¼ 1-{MaxPageSize} aralÄ±ÄŸÄ±nda olmalÄ±dÄ±r.");
        }

        if (!Enum.IsDefined(filter.Category))
            throw new BadRequestException("AxÄ±n filtri dÃ¼zgÃ¼n deyil.");

        if (!Enum.IsDefined(filter.Scope))
            throw new BadRequestException("AxÄ±n gÃ¶rÃ¼nÃ¼ÅŸÃ¼ dÃ¼zgÃ¼n deyil.");
    }

    private sealed record ActivityUserDetails(
        Guid Id,
        string UserName,
        string? ProfileImageUrl);

    private sealed record ActivityGameDetails(
        long GameId,
        string Name,
        string? Slug,
        string? CoverImageUrl);
}
