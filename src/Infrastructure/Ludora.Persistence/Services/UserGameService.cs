using Ludora.Application.DTOs;
using Ludora.Application.Common;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models.Notification;
using Ludora.Domain.Entities;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Ludora.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class UserGameService : IUserGameService
{
    private const int MaxPageSize = 50;
    private const int MaxProfileCandidateWindow = 5000;
    private const int SqlParameterBatchSize = 1000;
    private const int AzerbaijanUtcOffsetHours = 4;
    private readonly AppDbContext _context;
    private readonly IIgdbClient _igdbClient;
    private readonly INotificationService _notificationService;
    private readonly IAppCache _cache;

    public UserGameService(
        AppDbContext context,
        IIgdbClient igdbClient,
        INotificationService notificationService,
        IAppCache cache)
    {
        _context = context;
        _igdbClient = igdbClient;
        _notificationService = notificationService;
        _cache = cache;
    }

    public async Task<PagedResultDTO<UserGameDTO>> GetMineAsync(
        Guid userId,
        UserGameStatus? status = null,
        bool? isFavorite = null,
        bool? isWishlisted = null,
        PaginationFilterDTO? pagination = null,
        CancellationToken cancellationToken = default)
    {
        pagination ??= new PaginationFilterDTO();
        ValidatePagination(pagination.PageNumber, pagination.PageSize);

        var query = _context.UserGames
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (isFavorite.HasValue)
            query = query.Where(x => x.IsFavorite == isFavorite.Value);

        if (isWishlisted.HasValue)
            query = query.Where(x => x.IsWishlisted == isWishlisted.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDTO<UserGameDTO>
        {
            Items = await MapManyAsync(entities, cancellationToken),
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pagination.PageSize)
        };
    }

    public async Task<UserGameDashboardDTO> GetDashboardAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.UserGames
            .AsNoTracking()
            .Where(userGame => userGame.UserId == userId);

        var aggregate = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                PlayingCount = group.Count(
                    game => game.Status == UserGameStatus.Playing),
                PlanToPlayCount = group.Count(
                    game => game.Status == UserGameStatus.PlanToPlay),
                PlayedCount = group.Count(
                    game => game.Status == UserGameStatus.Played),
                DroppedCount = group.Count(
                    game => game.Status == UserGameStatus.Dropped),
                WishlistCount = group.Count(game => game.IsWishlisted),
                RatingCount = group.Count(game => game.Rating.HasValue),
                AverageRating = group.Average(game => game.Rating)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var ratingDistribution = await query
            .Where(game => game.Rating.HasValue)
            .GroupBy(game => game.Rating!.Value)
            .OrderBy(group => group.Key)
            .Select(group => new UserRatingDistributionDTO
            {
                Rating = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var recentEntity = await query
            .OrderByDescending(game => game.UpdatedAt ?? game.CreatedAt)
            .ThenByDescending(game => game.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var contentCounts = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(_ => new
            {
                ReviewCount = _context.Reviews.Count(
                    review =>
                        review.UserId == userId &&
                        review.Content.Trim() != string.Empty),
                GameListCount = _context.GameLists.Count(
                    gameList => gameList.UserId == userId)
            })
            .FirstAsync(cancellationToken);

        var statusCounts = new Dictionary<UserGameStatus, int>
        {
            [UserGameStatus.Playing] = aggregate?.PlayingCount ?? 0,
            [UserGameStatus.PlanToPlay] = aggregate?.PlanToPlayCount ?? 0,
            [UserGameStatus.Played] = aggregate?.PlayedCount ?? 0,
            [UserGameStatus.Dropped] = aggregate?.DroppedCount ?? 0
        };

        return new UserGameDashboardDTO
        {
            StatusCounts = statusCounts,
            WishlistCount = aggregate?.WishlistCount ?? 0,
            RatingCount = aggregate?.RatingCount ?? 0,
            AverageRating = aggregate?.AverageRating,
            RatingDistribution = ratingDistribution,
            RecentGame = recentEntity is null
                ? null
                : await MapAsync(recentEntity, cancellationToken),
            ReviewCount = contentCounts.ReviewCount,
            GameListCount = contentCounts.GameListCount
        };
    }

    public async Task<PagedResultDTO<UserGameDTO>> GetByUserAsync(
        Guid userId,
        UserGameFilterDTO filter,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ValidateFilter(filter);

        if (filter.IsWishlisted == true && currentUserId != userId)
        {
            throw new ForbiddenException(
                "Wishlist yalnız profil sahibinə görünür.");
        }

        var user = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.Id,
                x.IsPrivate
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        await EnsureCanViewUserGamesAsync(
            user.Id,
            user.IsPrivate,
            currentUserId,
            cancellationToken);

        var query = _context.UserGames
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (filter.IsFavorite.HasValue)
            query = query.Where(x => x.IsFavorite == filter.IsFavorite.Value);

        if (filter.IsWishlisted.HasValue)
            query = query.Where(x => x.IsWishlisted == filter.IsWishlisted.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var items = await MapManyAsync(entities, cancellationToken);

        return new PagedResultDTO<UserGameDTO>
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

    public Task<PagedResultDTO<UserGameDTO>> GetVisibleFavoritesAsync(
        Guid userId,
        Guid currentUserId,
        PaginationFilterDTO pagination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pagination);

        return GetByUserAsync(
            userId,
            new UserGameFilterDTO
            {
                IsFavorite = true,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize
            },
            currentUserId,
            cancellationToken);
    }

    public async Task<PagedResultDTO<ProfileGameEntryDTO>> GetProfileCollectionAsync(
        Guid userId,
        UserProfileGamesFilterDTO filter,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ValidateProfileGamesFilter(filter);

        var user = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.Id,
                x.IsPrivate
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        await EnsureCanViewUserGamesAsync(
            user.Id,
            user.IsPrivate,
            currentUserId,
            cancellationToken);

        var candidateQuery = BuildProfileCandidateQuery(userId);

        if (filter.Status.HasValue)
            candidateQuery = candidateQuery.Where(x => x.Status == filter.Status.Value);

        if (filter.Rating.HasValue)
            candidateQuery = candidateQuery.Where(x => x.Rating == filter.Rating.Value);

        var requiresCatalogMappingBeforePagination =
            !string.IsNullOrWhiteSpace(filter.Search) ||
            !string.IsNullOrWhiteSpace(filter.Genre) ||
            !string.IsNullOrWhiteSpace(filter.Company) ||
            !string.IsNullOrWhiteSpace(filter.Platform) ||
            !string.IsNullOrWhiteSpace(filter.PlayedPlatform) ||
            filter.ReleaseYear.HasValue ||
            filter.SortBy is UserProfileGameSortBy.RecentlyReleased or
                UserProfileGameSortBy.OldestRelease or
                UserProfileGameSortBy.NameAsc or
                UserProfileGameSortBy.NameDesc;

        if (!requiresCatalogMappingBeforePagination)
        {
            var pageTotalCount = await candidateQuery.CountAsync(cancellationToken);
            var candidatePage = await ApplyProfileCandidateSorting(
                    candidateQuery,
                    filter.SortBy)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);
            var reviewMap = await LoadProfileReviewsAsync(
                candidatePage,
                cancellationToken);
            var pageItems = await MapProfileGameEntriesAsync(
                ToUserGameEntities(candidatePage, userId),
                reviewMap,
                cancellationToken);

            return new PagedResultDTO<ProfileGameEntryDTO>
            {
                Items = pageItems,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalCount = pageTotalCount,
                TotalPages = pageTotalCount == 0
                    ? 0
                    : (int)Math.Ceiling(pageTotalCount / (double)filter.PageSize)
            };
        }

        var candidates = await ApplyProfileCandidateSorting(
                candidateQuery,
                UserProfileGameSortBy.RecentlyUpdated)
            .Take(MaxProfileCandidateWindow + 1)
            .ToListAsync(cancellationToken);

        if (candidates.Count > MaxProfileCandidateWindow)
        {
            throw new BadRequestException(
                $"Profil kolleksiyası çox böyükdür. Axtarışı dəqiqləşdirin; eyni anda ən çox {MaxProfileCandidateWindow} namizəd emal olunur.");
        }

        var reviewMapForCandidates = await LoadProfileReviewsAsync(
            candidates,
            cancellationToken);
        var candidateEntities = ToUserGameEntities(candidates, userId);

        var items = await MapProfileGameEntriesAsync(
            candidateEntities,
            reviewMapForCandidates,
            cancellationToken);

        items = ApplyProfileGameFilters(items, filter);
        items = ApplyProfileGameSorting(items, filter.SortBy);

        var totalCount = items.Count;
        var pagedItems = items
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return new PagedResultDTO<ProfileGameEntryDTO>
        {
            Items = pagedItems,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)filter.PageSize)
        };
    }

    public async Task<UserPlaytimeStatisticsDTO> GetPlaytimeStatisticsAsync(
        Guid userId,
        UserPlaytimePeriod period,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(period))
            throw new BadRequestException("Oyun vaxtı dövrü yanlışdır.");

        var user = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.Id,
                x.IsPrivate
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        await EnsureCanViewUserGamesAsync(
            user.Id,
            user.IsPrivate,
            currentUserId,
            cancellationToken);

        var reviewQuery = _context.Reviews
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted);

        var today = GetTodayInAzerbaijan();
        var chartRanges = BuildPlaytimeRanges(period, today);
        var chartStart = chartRanges[0].StartDate;
        var chartEnd = chartRanges[^1].EndDate;
        var chartStartUtc = chartStart
            .ToDateTime(TimeOnly.MinValue)
            .AddHours(-AzerbaijanUtcOffsetHours);
        var chartEndExclusiveUtc = chartEnd
            .AddDays(1)
            .ToDateTime(TimeOnly.MinValue)
            .AddHours(-AzerbaijanUtcOffsetHours);

        var chartReviews = await reviewQuery
            .Where(x =>
                (x.PlayedOn.HasValue &&
                 x.PlayedOn.Value >= chartStart &&
                 x.PlayedOn.Value <= chartEnd) ||
                (!x.PlayedOn.HasValue &&
                 x.CreatedAt >= chartStartUtc &&
                 x.CreatedAt < chartEndExclusiveUtc))
            .Select(x => new PlaytimeReviewRow(
                x.Source,
                x.GameId,
                x.PlayedOn,
                x.PlatformSource,
                x.PlatformId,
                x.PlaytimeMinutes ?? 0,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        var gameTotals = await reviewQuery
            .Where(x => x.PlaytimeMinutes.HasValue && x.PlaytimeMinutes > 0)
            .GroupBy(x => new { x.Source, x.GameId })
            .Select(group => new
            {
                group.Key.Source,
                group.Key.GameId,
                PlaytimeMinutes = group.Sum(x => (long)(x.PlaytimeMinutes ?? 0))
            })
            .ToListAsync(cancellationToken);

        var metadataRows = gameTotals
            .Select(x => new PlaytimeReviewRow(
                x.Source,
                x.GameId,
                null,
                null,
                null,
                0,
                DateTime.MinValue))
            .Concat(chartReviews)
            .GroupBy(x => (x.Source, x.GameId))
            .Select(x => x.First())
            .ToList();

        var gameMetadata = await ResolvePlaytimeGameMetadataAsync(
            metadataRows,
            cancellationToken);
        var chart = BuildPlaytimeChart(
            chartReviews,
            period,
            today,
            gameMetadata);
        var allTimeTotal = gameTotals.Sum(x => x.PlaytimeMinutes);

        if (gameTotals.Count == 0)
        {
            return new UserPlaytimeStatisticsDTO
            {
                Period = period,
                PeriodTotalPlaytimeMinutes = chart.Sum(x => x.PlaytimeMinutes),
                AllTimeTotalPlaytimeMinutes = 0,
                Chart = chart
            };
        }

        var topGameGroup = gameTotals
            .OrderByDescending(x => x.PlaytimeMinutes)
            .ThenBy(x => x.GameId)
            .First();

        gameMetadata.TryGetValue(
            (topGameGroup.Source, topGameGroup.GameId),
            out var topGameMetadata);

        var genreTotals = new Dictionary<string, double>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var gameTotal in gameTotals)
        {
            if (!gameMetadata.TryGetValue(
                    (gameTotal.Source, gameTotal.GameId),
                    out var metadata) ||
                metadata.Genres.Count == 0)
            {
                continue;
            }

            var share = gameTotal.PlaytimeMinutes /
                        (double)metadata.Genres.Count;

            foreach (var genre in metadata.Genres)
            {
                genreTotals[genre] =
                    genreTotals.GetValueOrDefault(genre) + share;
            }
        }

        var topGenre = genreTotals
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .FirstOrDefault();

        var platformPlaytimeRows = await reviewQuery
            .Where(x =>
                x.PlaytimeMinutes.HasValue &&
                x.PlaytimeMinutes > 0 &&
                x.PlatformSource.HasValue &&
                x.PlatformId.HasValue)
            .GroupBy(x => new
            {
                Source = x.PlatformSource!.Value,
                Id = x.PlatformId!.Value
            })
            .Select(group => new
            {
                group.Key.Source,
                group.Key.Id,
                PlaytimeMinutes = group.Sum(
                    x => (long)(x.PlaytimeMinutes ?? 0))
            })
            .ToListAsync(cancellationToken);

        var platformReferences = platformPlaytimeRows
            .Select(x => new CatalogReferenceDTO
            {
                Source = x.Source,
                Id = x.Id
            })
            .ToList();

        var platformMap = await ResolvePlatformLookupsAsync(
            platformReferences,
            cancellationToken);
        var platformTotals = new Dictionary<string, long>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var platformTotal in platformPlaytimeRows)
        {
            if (!platformMap.TryGetValue(
                    (platformTotal.Source, platformTotal.Id),
                    out var platform))
            {
                continue;
            }

            platformTotals[platform.Name] =
                platformTotals.GetValueOrDefault(platform.Name) +
                platformTotal.PlaytimeMinutes;
        }

        var topPlatform = platformTotals
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .FirstOrDefault();

        return new UserPlaytimeStatisticsDTO
        {
            Period = period,
            PeriodTotalPlaytimeMinutes = chart.Sum(x => x.PlaytimeMinutes),
            AllTimeTotalPlaytimeMinutes = allTimeTotal,
            Chart = chart,
            TopGame = new UserTopPlayedGameDTO
            {
                Source = topGameGroup.Source,
                GameId = topGameGroup.GameId,
                Name = topGameMetadata?.Name ?? $"Oyun #{topGameGroup.GameId}",
                CoverUrl = topGameMetadata?.CoverUrl,
                PlaytimeMinutes = topGameGroup.PlaytimeMinutes
            },
            TopGenre = string.IsNullOrWhiteSpace(topGenre.Key)
                ? null
                : new UserTopPlayedCategoryDTO
                {
                    Name = topGenre.Key,
                    PlaytimeMinutes = Math.Max(
                        1,
                        (long)Math.Round(
                            topGenre.Value,
                            MidpointRounding.AwayFromZero)),
                    Percentage = allTimeTotal == 0
                        ? 0
                        : Math.Round(
                            topGenre.Value / allTimeTotal * 100,
                            1)
                },
            TopPlatform = string.IsNullOrWhiteSpace(topPlatform.Key)
                ? null
                : new UserTopPlayedCategoryDTO
                {
                    Name = topPlatform.Key,
                    PlaytimeMinutes = topPlatform.Value,
                    Percentage = allTimeTotal == 0
                        ? 0
                        : Math.Round(
                            topPlatform.Value / (double)allTimeTotal * 100,
                            1)
                }
        };
    }

    public async Task<IReadOnlyList<WishlistedFriendDTO>> GetWishlistedFriendsAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");

        var followedUserIds = _context.UserFollows
            .AsNoTracking()
            .Where(x =>
                x.FollowerId == userId &&
                x.Status == FollowStatus.Accepted)
            .Select(x => x.FollowingId);

        var mutualFriendIds = _context.UserFollows
            .AsNoTracking()
            .Where(x =>
                x.FollowingId == userId &&
                x.Status == FollowStatus.Accepted &&
                followedUserIds.Contains(x.FollowerId))
            .Select(x => x.FollowerId);

        return await _context.UserGames
            .AsNoTracking()
            .Where(x =>
                mutualFriendIds.Contains(x.UserId) &&
                x.Source == source &&
                x.GameId == gameId &&
                x.IsWishlisted)
            .OrderBy(x => x.User.UserName)
            .Select(x => new WishlistedFriendDTO
            {
                UserId = x.UserId,
                UserName = x.User.UserName ?? string.Empty,
                ProfileImageUrl = x.User.ProfileImageUrl
            })
            .Take(6)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserGameDTO?> GetStateAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");

        var entity = await _context.UserGames
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.Source == source &&
                    x.GameId == gameId,
                cancellationToken);

        return entity is null
            ? null
            : await MapAsync(entity, cancellationToken);
    }

    public async Task<UserGameDTO> SetStatusAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        UserGameStatus status,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status))
            throw new BadRequestException("Oyun statusu yanlışdır.");

        await EnsureGameExistsAsync(source, gameId, cancellationToken);

        var entity = await FindAsync(
            userId,
            source,
            gameId,
            cancellationToken);

        if (entity is null)
        {
            entity = NewEntity(userId, source, gameId);
            entity.Status = status;
            await _context.UserGames.AddAsync(entity, cancellationToken);
        }
        else
        {
            if (entity.Status == status)
            {
                if (await EnsureTrackingLogAsync(
                        userId,
                        source,
                        gameId,
                        cancellationToken))
                {
                    await SaveAsync(cancellationToken);
                }

                return await MapAsync(entity, cancellationToken);
            }

            entity.Status = status;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await EnsureTrackingLogAsync(
            userId,
            source,
            gameId,
            cancellationToken);

        _context.AddUserActivity(
            userId,
            UserActivityType.GameStatusChanged,
            gameSource: source,
            gameId: gameId,
            status: status);

        await _notificationService.QueueForFollowersAsync(
            new FollowerNotificationRequest
            {
                ActorUserId = userId,
                Type = NotificationType.FollowingGameStatusChanged,
                EntityType = NotificationEntityType.Game,
                EntityId = $"{source}:{gameId}",
                Title = "Oyun statusu dəyişdi",
                Message = $"Oyun statusunu “{GetStatusLabel(status)}” olaraq yenilədi.",
                ActionUrl = $"/games/{source}/{gameId}"
            },
            cancellationToken);

        await SaveAsync(cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    public async Task<UserGameDTO?> RemoveStatusAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");

        var entity = await FindAsync(
            userId,
            source,
            gameId,
            cancellationToken);

        if (entity is null)
            return null;

        if (!entity.Status.HasValue)
            return await MapAsync(entity, cancellationToken);

        var previousStatus = entity.Status.Value;
        entity.Status = null;

        await RemoveTrackingLogIfEmptyAsync(
            userId,
            source,
            gameId,
            status: null,
            rating: entity.Rating,
            cancellationToken: cancellationToken);

        _context.AddUserActivity(
            userId,
            UserActivityType.GameStatusRemoved,
            gameSource: source,
            gameId: gameId,
            status: previousStatus);

        return await SaveOrDeleteAsync(entity, cancellationToken);
    }

    public async Task<UserGameDTO?> SetFavoriteAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        bool isFavorite,
        CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");

        var entity = await FindAsync(
            userId,
            source,
            gameId,
            cancellationToken);

        if (entity is null)
        {
            if (!isFavorite)
                return null;

            await EnsureGameExistsAsync(source, gameId, cancellationToken);
            entity = NewEntity(userId, source, gameId);
            entity.IsFavorite = true;
            await _context.UserGames.AddAsync(entity, cancellationToken);
        }
        else
        {
            if (entity.IsFavorite == isFavorite)
                return await MapAsync(entity, cancellationToken);

            entity.IsFavorite = isFavorite;
        }

        _context.AddUserActivity(
            userId,
            isFavorite
                ? UserActivityType.GameAddedToFavorites
                : UserActivityType.GameRemovedFromFavorites,
            gameSource: source,
            gameId: gameId);

        return await SaveOrDeleteAsync(entity, cancellationToken);
    }

    public async Task<UserGameDTO?> SetWishlistAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        bool isWishlisted,
        CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");

        var entity = await FindAsync(
            userId,
            source,
            gameId,
            cancellationToken);

        if (entity is null)
        {
            if (!isWishlisted)
                return null;

            await EnsureGameExistsAsync(source, gameId, cancellationToken);
            entity = NewEntity(userId, source, gameId);
            entity.IsWishlisted = true;
            await _context.UserGames.AddAsync(entity, cancellationToken);
        }
        else
        {
            if (entity.IsWishlisted == isWishlisted)
                return await MapAsync(entity, cancellationToken);

            entity.IsWishlisted = isWishlisted;
        }

        _context.AddUserActivity(
            userId,
            isWishlisted
                ? UserActivityType.GameAddedToWishlist
                : UserActivityType.GameRemovedFromWishlist,
            gameSource: source,
            gameId: gameId);

        return await SaveOrDeleteAsync(entity, cancellationToken);
    }

    public async Task<UserGameDTO?> SetRatingAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        double? rating,
        CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");
        ValidateRating(rating);

        var entity = await FindAsync(
            userId,
            source,
            gameId,
            cancellationToken);

        if (entity is null)
        {
            if (!rating.HasValue)
            {
                if (await RemoveTrackingLogIfEmptyAsync(
                        userId,
                        source,
                        gameId,
                        status: null,
                        rating: null,
                        cancellationToken: cancellationToken))
                {
                    await SaveAsync(cancellationToken);
                }

                return null;
            }

            await EnsureGameExistsAsync(source, gameId, cancellationToken);
            entity = NewEntity(userId, source, gameId);
            entity.Rating = rating.Value;
            await _context.UserGames.AddAsync(entity, cancellationToken);
        }
        else
        {
            if (entity.Rating == rating)
            {
                if (rating.HasValue &&
                    await EnsureTrackingLogAsync(
                        userId,
                        source,
                        gameId,
                        cancellationToken))
                {
                    await SaveAsync(cancellationToken);
                }

                return await MapAsync(entity, cancellationToken);
            }

            entity.Rating = rating;
        }

        if (rating.HasValue)
        {
            await EnsureTrackingLogAsync(
                userId,
                source,
                gameId,
                cancellationToken);
        }
        else
        {
            await RemoveTrackingLogIfEmptyAsync(
                userId,
                source,
                gameId,
                entity.Status,
                rating: null,
                cancellationToken: cancellationToken);
        }

        _context.AddUserActivity(
            userId,
            rating.HasValue
                ? UserActivityType.GameRated
                : UserActivityType.GameRatingRemoved,
            gameSource: source,
            gameId: gameId,
            rating: rating);

        return await SaveOrDeleteAsync(entity, cancellationToken);
    }

    private IQueryable<ProfileGameCandidateRow> BuildProfileCandidateQuery(
        Guid userId)
    {
        var userGames = _context.UserGames
            .AsNoTracking()
            .Where(x => x.UserId == userId);
        var reviews = _context.Reviews
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted);

        var userGameCandidates = userGames
            .Select(userGame => new ProfileGameCandidateRow
            {
                Source = userGame.Source,
                GameId = userGame.GameId,
                Status = userGame.Status,
                IsFavorite = userGame.IsFavorite,
                IsWishlisted = userGame.IsWishlisted,
                Rating = userGame.Rating,
                CreatedAt = userGame.CreatedAt,
                UpdatedAt = userGame.UpdatedAt,
                ReviewId = reviews
                    .Where(review =>
                        review.Source == userGame.Source &&
                        review.GameId == userGame.GameId)
                    .OrderByDescending(review =>
                        review.UpdatedAt ?? review.CreatedAt)
                    .ThenByDescending(review => review.Id)
                    .Select(review => (long?)review.Id)
                    .FirstOrDefault(),
                PlayedOn = reviews
                    .Where(review =>
                        review.Source == userGame.Source &&
                        review.GameId == userGame.GameId)
                    .OrderByDescending(review =>
                        review.UpdatedAt ?? review.CreatedAt)
                    .ThenByDescending(review => review.Id)
                    .Select(review => review.PlayedOn)
                    .FirstOrDefault()
            });

        var reviewOnlyCandidates = reviews
            .Where(review =>
                !userGames.Any(userGame =>
                    userGame.Source == review.Source &&
                    userGame.GameId == review.GameId) &&
                !reviews.Any(other =>
                    other.Source == review.Source &&
                    other.GameId == review.GameId &&
                    ((other.UpdatedAt ?? other.CreatedAt) >
                     (review.UpdatedAt ?? review.CreatedAt) ||
                     ((other.UpdatedAt ?? other.CreatedAt) ==
                      (review.UpdatedAt ?? review.CreatedAt) &&
                      other.Id > review.Id))))
            .Select(review => new ProfileGameCandidateRow
            {
                Source = review.Source,
                GameId = review.GameId,
                Status = null,
                IsFavorite = false,
                IsWishlisted = false,
                Rating = null,
                CreatedAt = review.CreatedAt,
                UpdatedAt = review.UpdatedAt ?? review.CreatedAt,
                ReviewId = review.Id,
                PlayedOn = review.PlayedOn
            });

        return userGameCandidates
            .Concat(reviewOnlyCandidates)
            .Where(x =>
                x.Status.HasValue ||
                x.Rating.HasValue ||
                x.ReviewId.HasValue);
    }

    private static IOrderedQueryable<ProfileGameCandidateRow>
        ApplyProfileCandidateSorting(
            IQueryable<ProfileGameCandidateRow> query,
            UserProfileGameSortBy sortBy)
    {
        return sortBy switch
        {
            UserProfileGameSortBy.RatingHighToLow => query
                .OrderByDescending(x => x.Rating ?? -1)
                .ThenBy(x => x.Source)
                .ThenBy(x => x.GameId),
            UserProfileGameSortBy.RatingLowToHigh => query
                .OrderBy(x => x.Rating ?? 11)
                .ThenBy(x => x.Source)
                .ThenBy(x => x.GameId),
            UserProfileGameSortBy.RecentlyPlayed => query
                .OrderByDescending(x => x.PlayedOn ?? DateOnly.MinValue)
                .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .ThenBy(x => x.Source)
                .ThenBy(x => x.GameId),
            _ => query
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .ThenByDescending(x => x.CreatedAt)
                .ThenBy(x => x.Source)
                .ThenBy(x => x.GameId)
        };
    }

    private async Task<Dictionary<
        (CatalogSource Source, long GameId),
        Review>> LoadProfileReviewsAsync(
        IReadOnlyCollection<ProfileGameCandidateRow> candidates,
        CancellationToken cancellationToken)
    {
        var reviewIds = candidates
            .Where(x => x.ReviewId.HasValue)
            .Select(x => x.ReviewId!.Value)
            .Distinct()
            .ToArray();

        if (reviewIds.Length == 0)
            return [];

        var reviews = new List<Review>(reviewIds.Length);

        foreach (var batch in reviewIds.Chunk(SqlParameterBatchSize))
        {
            reviews.AddRange(await _context.Reviews
                .AsNoTracking()
                .Where(x => batch.Contains(x.Id))
                .ToListAsync(cancellationToken));
        }

        var reviewById = reviews.ToDictionary(x => x.Id);

        return candidates
            .Where(x =>
                x.ReviewId.HasValue &&
                reviewById.ContainsKey(x.ReviewId.Value))
            .ToDictionary(
                x => (x.Source, x.GameId),
                x => reviewById[x.ReviewId!.Value]);
    }

    private static List<UserGame> ToUserGameEntities(
        IReadOnlyCollection<ProfileGameCandidateRow> candidates,
        Guid userId)
    {
        return candidates
            .Select(x => new UserGame
            {
                UserId = userId,
                Source = x.Source,
                GameId = x.GameId,
                Status = x.Status,
                IsFavorite = x.IsFavorite,
                IsWishlisted = x.IsWishlisted,
                Rating = x.Rating,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToList();
    }

    private async Task<List<ProfileGameEntryDTO>> MapProfileGameEntriesAsync(
        IReadOnlyCollection<UserGame> entities,
        IReadOnlyDictionary<(CatalogSource Source, long GameId), Review> reviewMap,
        CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
            return [];

        var localIds = entities
            .Where(x => x.Source == CatalogSource.Local && x.GameId <= int.MaxValue)
            .Select(x => (int)x.GameId)
            .Distinct()
            .ToList();

        var localGames = await _context.Games
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.GameGenres)
            .Include(x => x.GamePlatforms)
            .Include(x => x.GameCompanies)
            .Where(x => localIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var genreRefs = localGames.Values
            .SelectMany(x => x.GameGenres)
            .Select(x => new CatalogReferenceDTO { Source = x.GenreSource, Id = x.GenreSourceId })
            .ToList();

        var platformRefs = localGames.Values
            .SelectMany(x => x.GamePlatforms)
            .Select(x => new CatalogReferenceDTO { Source = x.PlatformSource, Id = x.PlatformSourceId })
            .ToList();

        var companyRefs = localGames.Values
            .SelectMany(x => x.GameCompanies)
            .Select(x => new CatalogReferenceDTO { Source = x.CompanySource, Id = x.CompanySourceId })
            .ToList();

        var playedPlatformRefs = reviewMap.Values
            .Where(x => x.PlatformSource.HasValue && x.PlatformId.HasValue)
            .Select(x => new CatalogReferenceDTO
            {
                Source = x.PlatformSource!.Value,
                Id = x.PlatformId!.Value
            })
            .ToList();

        var genreMap = await ResolveGenreLookupsAsync(genreRefs, cancellationToken);
        var platformMap = await ResolvePlatformLookupsAsync(platformRefs.Concat(playedPlatformRefs).ToList(), cancellationToken);
        var companyMap = await ResolveCompanyLookupsAsync(companyRefs, cancellationToken);

        var igdbIds = entities
            .Where(x => x.Source == CatalogSource.Igdb)
            .Select(x => x.GameId)
            .Distinct()
            .ToArray();

        var igdbGames = new Dictionary<long, Ludora.Application.Models.IgdbGameModel>();

        if (igdbIds.Length > 0)
        {
            try
            {
                igdbGames = (await _igdbClient.GetGamesByIdsAsync(igdbIds, cancellationToken))
                    .ToDictionary(x => x.Id, x => x);
            }
            catch (ExternalServiceException)
            {
            }
        }

        var result = new List<ProfileGameEntryDTO>(entities.Count);

        foreach (var entity in entities)
        {
            reviewMap.TryGetValue((entity.Source, entity.GameId), out var review);
            var playedPlatformName = review is not null && review.PlatformSource.HasValue && review.PlatformId.HasValue
                ? platformMap.GetValueOrDefault((review.PlatformSource.Value, review.PlatformId.Value))?.Name
                : null;

            if (entity.Source == CatalogSource.Local && localGames.TryGetValue((int)entity.GameId, out var localGame))
            {
                result.Add(new ProfileGameEntryDTO
                {
                    Source = entity.Source,
                    GameId = entity.GameId,
                    Name = localGame.Name,
                    Slug = localGame.Slug,
                    CoverUrl = localGame.CoverImageUrl,
                    ReleaseDate = localGame.ReleaseDate,
                    Status = entity.Status,
                    IsFavorite = entity.IsFavorite,
                    IsWishlisted = entity.IsWishlisted,
                    Rating = entity.Rating,
                    ReviewId = review?.Id,
                    PlayedOn = review?.PlayedOn,
                    PlaytimeMinutes = review?.PlaytimeMinutes,
                    PlayedPlatformName = playedPlatformName,
                    Genres = localGame.GameGenres
                        .Select(x => genreMap.GetValueOrDefault((x.GenreSource, x.GenreSourceId))?.Name)
                        .Where(static x => !string.IsNullOrWhiteSpace(x))
                        .Select(static x => x!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToList(),
                    Platforms = localGame.GamePlatforms
                        .Select(x => platformMap.GetValueOrDefault((x.PlatformSource, x.PlatformSourceId))?.Name)
                        .Where(static x => !string.IsNullOrWhiteSpace(x))
                        .Select(static x => x!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToList(),
                    Companies = localGame.GameCompanies
                        .Select(x => companyMap.GetValueOrDefault((x.CompanySource, x.CompanySourceId))?.Name)
                        .Where(static x => !string.IsNullOrWhiteSpace(x))
                        .Select(static x => x!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToList(),
                    CreatedAt = entity.CreatedAt,
                    UpdatedAt = entity.UpdatedAt
                });

                continue;
            }

            if (entity.Source == CatalogSource.Igdb && igdbGames.TryGetValue(entity.GameId, out var igdbGame))
            {
                result.Add(new ProfileGameEntryDTO
                {
                    Source = entity.Source,
                    GameId = entity.GameId,
                    Name = igdbGame.Name,
                    Slug = null,
                    CoverUrl = igdbGame.CoverImageUrl,
                    ReleaseDate = igdbGame.ReleaseDate,
                    Status = entity.Status,
                    IsFavorite = entity.IsFavorite,
                    IsWishlisted = entity.IsWishlisted,
                    Rating = entity.Rating,
                    ReviewId = review?.Id,
                    PlayedOn = review?.PlayedOn,
                    PlaytimeMinutes = review?.PlaytimeMinutes,
                    PlayedPlatformName = playedPlatformName,
                    Genres = igdbGame.Genres.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
                    Platforms = igdbGame.Platforms.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
                    Companies = igdbGame.Companies.Select(x => x.Company.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList(),
                    CreatedAt = entity.CreatedAt,
                    UpdatedAt = entity.UpdatedAt
                });
            }
        }

        return result;
    }

    private static List<ProfileGameEntryDTO> ApplyProfileGameFilters(
        List<ProfileGameEntryDTO> items,
        UserProfileGamesFilterDTO filter)
    {
        IEnumerable<ProfileGameEntryDTO> query = items;

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Genre))
        {
            var genre = filter.Genre.Trim();
            query = query.Where(x => x.Genres.Any(item => item.Equals(genre, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Company))
        {
            var company = filter.Company.Trim();
            query = query.Where(x => x.Companies.Any(item => item.Equals(company, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Platform))
        {
            var platform = filter.Platform.Trim();
            query = query.Where(x => x.Platforms.Any(item => item.Equals(platform, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(filter.PlayedPlatform))
        {
            var playedPlatform = filter.PlayedPlatform.Trim();
            query = query.Where(x => string.Equals(x.PlayedPlatformName, playedPlatform, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.Status.HasValue)
            query = query.Where(x => x.Status == filter.Status.Value);

        if (filter.Rating.HasValue)
            query = query.Where(x => x.Rating == filter.Rating.Value);

        if (filter.ReleaseYear.HasValue)
            query = query.Where(x => x.ReleaseDate?.Year == filter.ReleaseYear.Value);

        return query.ToList();
    }

    private static List<ProfileGameEntryDTO> ApplyProfileGameSorting(
        List<ProfileGameEntryDTO> items,
        UserProfileGameSortBy sortBy)
    {
        IOrderedEnumerable<ProfileGameEntryDTO> ordered = sortBy switch
        {
            UserProfileGameSortBy.RecentlyReleased => items.OrderByDescending(x => x.ReleaseDate ?? DateOnly.MinValue).ThenBy(x => x.Name),
            UserProfileGameSortBy.OldestRelease => items.OrderBy(x => x.ReleaseDate ?? DateOnly.MaxValue).ThenBy(x => x.Name),
            UserProfileGameSortBy.RatingHighToLow => items.OrderByDescending(x => x.Rating ?? double.MinValue).ThenBy(x => x.Name),
            UserProfileGameSortBy.RatingLowToHigh => items.OrderBy(x => x.Rating ?? double.MaxValue).ThenBy(x => x.Name),
            UserProfileGameSortBy.NameAsc => items.OrderBy(x => x.Name).ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt),
            UserProfileGameSortBy.NameDesc => items.OrderByDescending(x => x.Name).ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt),
            UserProfileGameSortBy.RecentlyPlayed => items
                .OrderByDescending(x => x.PlayedOn ?? DateOnly.MinValue)
                .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt),
            _ => items.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ThenByDescending(x => x.CreatedAt)
        };

        return ordered.ToList();
    }

    private static IReadOnlyList<UserPlaytimeChartPointDTO> BuildPlaytimeChart(
        IReadOnlyCollection<PlaytimeReviewRow> reviews,
        UserPlaytimePeriod period,
        DateOnly today,
        IReadOnlyDictionary<
            (CatalogSource Source, long GameId),
            PlaytimeGameMetadata> gameMetadata)
    {
        var ranges = BuildPlaytimeRanges(period, today);

        return ranges
            .Select(range =>
            {
                var rangeReviews = reviews
                    .Where(review =>
                    {
                        var activityDate = review.PlayedOn ??
                            DateOnly.FromDateTime(
                                review.ActivityAtUtc.AddHours(
                                    AzerbaijanUtcOffsetHours));

                        return activityDate >= range.StartDate &&
                               activityDate <= range.EndDate;
                    })
                    .ToList();

                var games = rangeReviews
                    .GroupBy(review => (review.Source, review.GameId))
                    .Select(group =>
                    {
                        gameMetadata.TryGetValue(
                            group.Key,
                            out var metadata);

                        return new UserPlaytimeChartGameDTO
                        {
                            Source = group.Key.Source,
                            GameId = group.Key.GameId,
                            Name = metadata?.Name ?? $"Oyun #{group.Key.GameId}",
                            CoverUrl = metadata?.CoverUrl,
                            PlaytimeMinutes = group.Sum(
                                review => (long)review.PlaytimeMinutes)
                        };
                    })
                    .Where(game => game.PlaytimeMinutes > 0)
                    .OrderByDescending(game => game.PlaytimeMinutes)
                    .ThenBy(game => game.Name)
                    .ToList();

                return new UserPlaytimeChartPointDTO
                {
                    StartDate = range.StartDate,
                    EndDate = range.EndDate,
                    PlaytimeMinutes = games.Sum(game => game.PlaytimeMinutes),
                    GameCount = games.Count,
                    Games = games
                };
            })
            .ToList();
    }

    private static IReadOnlyList<PlaytimeRange> BuildPlaytimeRanges(
        UserPlaytimePeriod period,
        DateOnly today)
    {
        if (period == UserPlaytimePeriod.Weekly)
        {
            var daysSinceMonday =
                ((int)today.DayOfWeek + 6) % 7;
            var weekStart = today.AddDays(-daysSinceMonday);

            return Enumerable.Range(0, 7)
                .Select(offset =>
                {
                    var date = weekStart.AddDays(offset);
                    return new PlaytimeRange(date, date);
                })
                .ToList();
        }

        if (period == UserPlaytimePeriod.Monthly)
        {
            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var monthEnd = monthStart
                .AddMonths(1)
                .AddDays(-1);
            var ranges = new List<PlaytimeRange>();

            for (var start = monthStart; start <= monthEnd; start = start.AddDays(7))
            {
                var end = start.AddDays(6);
                ranges.Add(new PlaytimeRange(
                    start,
                    end > monthEnd ? monthEnd : end));
            }

            return ranges;
        }

        return Enumerable.Range(1, 12)
            .Select(month =>
            {
                var start = new DateOnly(today.Year, month, 1);
                return new PlaytimeRange(
                    start,
                    start.AddMonths(1).AddDays(-1));
            })
            .ToList();
    }

    private async Task<Dictionary<(CatalogSource Source, long GameId), PlaytimeGameMetadata>> ResolvePlaytimeGameMetadataAsync(
        IReadOnlyCollection<PlaytimeReviewRow> reviews,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<
            (CatalogSource Source, long GameId),
            PlaytimeGameMetadata>();

        var localIds = reviews
            .Where(x =>
                x.Source == CatalogSource.Local &&
                x.GameId <= int.MaxValue)
            .Select(x => (int)x.GameId)
            .Distinct()
            .ToArray();

        if (localIds.Length > 0)
        {
            var localGames = await _context.Games
                .AsNoTracking()
                .Include(x => x.GameGenres)
                .Where(x => localIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            var genreReferences = localGames
                .SelectMany(x => x.GameGenres)
                .Select(x => new CatalogReferenceDTO
                {
                    Source = x.GenreSource,
                    Id = x.GenreSourceId
                })
                .ToList();

            var genreMap = await ResolveGenreLookupsAsync(
                genreReferences,
                cancellationToken);

            foreach (var game in localGames)
            {
                var genres = game.GameGenres
                    .Select(x => genreMap.GetValueOrDefault(
                        (x.GenreSource, x.GenreSourceId))?.Name)
                    .Where(static x => !string.IsNullOrWhiteSpace(x))
                    .Select(static x => x!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList();

                result[(CatalogSource.Local, game.Id)] =
                    new PlaytimeGameMetadata(
                        game.Name,
                        game.CoverImageUrl,
                        genres);
            }
        }

        var igdbIds = reviews
            .Where(x => x.Source == CatalogSource.Igdb)
            .Select(x => x.GameId)
            .Distinct()
            .ToArray();

        if (igdbIds.Length == 0)
            return result;

        try
        {
            var igdbGames = await _igdbClient.GetGamesByIdsAsync(
                igdbIds,
                cancellationToken);

            foreach (var game in igdbGames)
            {
                result[(CatalogSource.Igdb, game.Id)] =
                    new PlaytimeGameMetadata(
                        game.Name,
                        game.CoverImageUrl,
                        game.Genres
                            .Select(x => x.Name)
                            .Where(static x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(x => x)
                            .ToList());
            }
        }
        catch (ExternalServiceException)
        {
        }

        return result;
    }

    private async Task<Dictionary<(CatalogSource Source, long Id), LookupItem>> ResolveGenreLookupsAsync(
        IReadOnlyCollection<CatalogReferenceDTO> references,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<(CatalogSource Source, long Id), LookupItem>();
        var localIds = references.Where(x => x.Source == CatalogSource.Local).Select(x => (int)x.Id).Distinct().ToArray();

        if (localIds.Length > 0)
        {
            var locals = await _context.Genres
                .AsNoTracking()
                .Where(x => localIds.Contains(x.Id))
                .Select(x => new LookupItem(CatalogSource.Local, x.Id, x.Name))
                .ToListAsync(cancellationToken);

            foreach (var item in locals)
                result[(item.Source, item.Id)] = item;
        }

        var igdbIds = references.Where(x => x.Source == CatalogSource.Igdb).Select(x => x.Id).Distinct().ToArray();
        if (igdbIds.Length == 0)
            return result;

        try
        {
            var items = await _igdbClient.GetGenresByIdsAsync(igdbIds, cancellationToken);
            foreach (var item in items)
                result[(CatalogSource.Igdb, item.Id)] = new LookupItem(CatalogSource.Igdb, item.Id, item.Name);
        }
        catch (ExternalServiceException)
        {
        }

        return result;
    }

    private async Task<Dictionary<(CatalogSource Source, long Id), LookupItem>> ResolvePlatformLookupsAsync(
        IReadOnlyCollection<CatalogReferenceDTO> references,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<(CatalogSource Source, long Id), LookupItem>();
        var localIds = references.Where(x => x.Source == CatalogSource.Local).Select(x => (int)x.Id).Distinct().ToArray();

        if (localIds.Length > 0)
        {
            var locals = await _context.Platforms
                .AsNoTracking()
                .Where(x => localIds.Contains(x.Id))
                .Select(x => new LookupItem(CatalogSource.Local, x.Id, x.Name))
                .ToListAsync(cancellationToken);

            foreach (var item in locals)
                result[(item.Source, item.Id)] = item;
        }

        var igdbIds = references.Where(x => x.Source == CatalogSource.Igdb).Select(x => x.Id).Distinct().ToArray();
        if (igdbIds.Length == 0)
            return result;

        try
        {
            var items = await _igdbClient.GetPlatformsByIdsAsync(igdbIds, cancellationToken);
            foreach (var item in items)
                result[(CatalogSource.Igdb, item.Id)] = new LookupItem(CatalogSource.Igdb, item.Id, item.Name);
        }
        catch (ExternalServiceException)
        {
        }

        return result;
    }

    private async Task<Dictionary<(CatalogSource Source, long Id), LookupItem>> ResolveCompanyLookupsAsync(
        IReadOnlyCollection<CatalogReferenceDTO> references,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<(CatalogSource Source, long Id), LookupItem>();
        var localIds = references.Where(x => x.Source == CatalogSource.Local).Select(x => (int)x.Id).Distinct().ToArray();

        if (localIds.Length > 0)
        {
            var locals = await _context.Companies
                .AsNoTracking()
                .Where(x => localIds.Contains(x.Id))
                .Select(x => new LookupItem(CatalogSource.Local, x.Id, x.Name))
                .ToListAsync(cancellationToken);

            foreach (var item in locals)
                result[(item.Source, item.Id)] = item;
        }

        var igdbIds = references.Where(x => x.Source == CatalogSource.Igdb).Select(x => x.Id).Distinct().ToArray();
        if (igdbIds.Length == 0)
            return result;

        try
        {
            var items = await _igdbClient.GetCompaniesByIdsAsync(igdbIds, cancellationToken);
            foreach (var item in items)
                result[(CatalogSource.Igdb, item.Id)] = new LookupItem(CatalogSource.Igdb, item.Id, item.Name);
        }
        catch (ExternalServiceException)
        {
        }

        return result;
    }

    private static void ValidateProfileGamesFilter(UserProfileGamesFilterDTO filter)
    {
        if (filter.PageNumber < 1)
            throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");

        if (filter.PageSize < 1 || filter.PageSize > MaxPageSize)
            throw new BadRequestException($"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");

        if (filter.Rating.HasValue)
            ValidateRating(filter.Rating);

        if (filter.ReleaseYear.HasValue && filter.ReleaseYear.Value < 1950)
            throw new BadRequestException("Buraxılış ili düzgün deyil.");

        if (filter.Status.HasValue && !Enum.IsDefined(filter.Status.Value))
            throw new BadRequestException("Oyun statusu yanlışdır.");

        if (!Enum.IsDefined(filter.SortBy))
            throw new BadRequestException("Sıralama meyarı yanlışdır.");
    }

    private async Task<bool> EnsureTrackingLogAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        CancellationToken cancellationToken)
    {
        var log = await _context.Reviews
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.Source == source &&
                    x.GameId == gameId,
                cancellationToken);

        if (log is not null && !log.IsDeleted)
            return false;

        var now = DateTime.UtcNow;

        if (log is null)
        {
            await _context.Reviews.AddAsync(
                new Review
                {
                    UserId = userId,
                    Source = source,
                    GameId = gameId,
                    Content = string.Empty,
                    ContainsSpoilers = false,
                    PlayedOn = GetTodayInAzerbaijan(),
                    CreatedAt = now
                },
                cancellationToken);
            return true;
        }

        log.Content = string.Empty;
        log.ContainsSpoilers = false;
        log.PlayedOn = GetTodayInAzerbaijan();
        log.PlatformSource = null;
        log.PlatformId = null;
        log.PlaytimeMinutes = null;
        log.CreatedAt = now;
        log.UpdatedAt = null;
        log.IsDeleted = false;
        log.DeletedAtUtc = null;
        return true;
    }

    private async Task<bool> RemoveTrackingLogIfEmptyAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        UserGameStatus? status,
        double? rating,
        CancellationToken cancellationToken)
    {
        if (status.HasValue || rating.HasValue)
            return false;

        var log = await _context.Reviews
            .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.Source == source &&
                    x.GameId == gameId,
                cancellationToken);

        if (log is null ||
            !string.IsNullOrWhiteSpace(log.Content) ||
            log.PlatformSource.HasValue ||
            log.PlatformId.HasValue ||
            log.PlaytimeMinutes is > 0)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        log.IsDeleted = true;
        log.DeletedAtUtc = now;
        log.UpdatedAt = now;

        var likes = await _context.ReviewLikes
            .Where(x => x.ReviewId == log.Id)
            .ToListAsync(cancellationToken);
        _context.ReviewLikes.RemoveRange(likes);

        var comments = await _context.ReviewComments
            .Where(x => x.ReviewId == log.Id)
            .ToListAsync(cancellationToken);

        foreach (var comment in comments)
        {
            comment.IsDeleted = true;
            comment.DeletedAtUtc = now;
            comment.UpdatedAt = now;
        }

        return true;
    }

    private async Task<UserGameDTO?> SaveOrDeleteAsync(
        UserGame entity,
        CancellationToken cancellationToken)
    {
        if (IsEmpty(entity))
        {
            _context.UserGames.Remove(entity);
            await SaveAsync(cancellationToken);
            return null;
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await SaveAsync(cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await GameCatalogCache.InvalidateAsync(
                _cache,
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation())
        {
            throw new ConflictException(
                "Bu oyun üçün istifadəçi məlumatı artıq mövcuddur.");
        }
    }

    private Task<UserGame?> FindAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        CancellationToken cancellationToken)
    {
        return _context.UserGames.FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.Source == source &&
                x.GameId == gameId,
            cancellationToken);
    }

    private async Task EnsureGameExistsAsync(
        CatalogSource source,
        long gameId,
        CancellationToken cancellationToken)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");

        if (source == CatalogSource.Local)
        {
            if (gameId > int.MaxValue ||
                !await _context.Games.AnyAsync(
                    x => x.Id == (int)gameId,
                    cancellationToken))
            {
                throw new NotFoundException("Oyun tapılmadı.");
            }

            return;
        }

        if (source == CatalogSource.Igdb)
        {
            _ = await _igdbClient.GetGameByIdAsync(
                    gameId,
                    cancellationToken)
                ?? throw new NotFoundException("IGDB oyunu tapılmadı.");

            return;
        }

        throw new BadRequestException("Oyun mənbəyi yanlışdır.");
    }

    private async Task EnsureCanViewUserGamesAsync(
        Guid userId,
        bool isPrivate,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId == userId)
            return;

        if (currentUserId.HasValue)
        {
            var hasBlock = await _context.UserBlocks
                .AsNoTracking()
                .AnyAsync(x =>
                        x.BlockerId == currentUserId.Value &&
                        x.BlockedUserId == userId ||
                        x.BlockerId == userId &&
                        x.BlockedUserId == currentUserId.Value,
                    cancellationToken);

            if (hasBlock)
            {
                throw new ForbiddenException(
                    "Bu istifadəçinin oyunlarına baxmaq mümkün deyil.");
            }
        }

        if (!isPrivate)
            return;

        var isFollowing = currentUserId.HasValue &&
                          await _context.UserFollows
                              .AsNoTracking()
                              .AnyAsync(x =>
                                      x.FollowerId == currentUserId.Value &&
                                      x.FollowingId == userId &&
                                      x.Status == FollowStatus.Accepted,
                                  cancellationToken);

        if (!isFollowing)
            throw new ForbiddenException("Bu profil gizlidir.");
    }

    private async Task<IReadOnlyList<UserGameDTO>> MapManyAsync(
        List<UserGame> entities,
        CancellationToken cancellationToken)
    {
        var localIds = entities
            .Where(x =>
                x.Source == CatalogSource.Local &&
                x.GameId <= int.MaxValue)
            .Select(x => (int)x.GameId)
            .Distinct()
            .ToList();

        var localGames = await _context.Games
            .AsNoTracking()
            .Where(x => localIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var result = new List<UserGameDTO>(entities.Count);

        foreach (var entity in entities)
        {
            if (entity.Source == CatalogSource.Local &&
                localGames.TryGetValue((int)entity.GameId, out var localGame))
            {
                result.Add(Map(
                    entity,
                    localGame.Name,
                    localGame.Slug,
                    localGame.CoverImageUrl,
                    localGame.ReleaseDate));
            }
            else if (entity.Source == CatalogSource.Igdb)
            {
                var game = await _igdbClient.GetGameByIdAsync(
                    entity.GameId,
                    cancellationToken);

                if (game is not null)
                {
                    result.Add(Map(
                        entity,
                        game.Name,
                        null,
                        game.CoverImageUrl,
                        game.ReleaseDate));
                }
            }
        }

        return result;
    }

    private async Task<UserGameDTO> MapAsync(
        UserGame entity,
        CancellationToken cancellationToken)
    {
        if (entity.Source == CatalogSource.Local)
        {
            var game = await _context.Games
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == (int)entity.GameId,
                    cancellationToken)
                ?? throw new NotFoundException("Oyun tapılmadı.");

            return Map(
                entity,
                game.Name,
                game.Slug,
                game.CoverImageUrl,
                game.ReleaseDate);
        }

        var igdbGame = await _igdbClient.GetGameByIdAsync(
                entity.GameId,
                cancellationToken)
            ?? throw new NotFoundException("IGDB oyunu tapılmadı.");

        return Map(
            entity,
            igdbGame.Name,
            null,
            igdbGame.CoverImageUrl,
            igdbGame.ReleaseDate);
    }

    private static UserGameDTO Map(
        UserGame entity,
        string name,
        string? slug,
        string? coverImageUrl,
        DateOnly? releaseDate)
    {
        return new UserGameDTO
        {
            Source = entity.Source,
            GameId = entity.GameId,
            Name = name,
            Slug = slug,
            CoverUrl = coverImageUrl,
            ReleaseDate = releaseDate,
            Status = entity.Status,
            IsFavorite = entity.IsFavorite,
            IsWishlisted = entity.IsWishlisted,
            Rating = entity.Rating,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static UserGame NewEntity(
        Guid userId,
        CatalogSource source,
        long gameId)
    {
        return new UserGame
        {
            UserId = userId,
            Source = source,
            GameId = gameId,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static bool IsEmpty(UserGame entity)
    {
        return entity.Status is null &&
               !entity.IsFavorite &&
               !entity.IsWishlisted &&
               !entity.Rating.HasValue;
    }

    private static void ValidateFilter(UserGameFilterDTO filter)
    {
        if (filter.PageNumber < 1)
            throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");

        if (filter.PageSize < 1 || filter.PageSize > MaxPageSize)
        {
            throw new BadRequestException(
                $"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");
        }
    }

    private static void ValidatePagination(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new BadRequestException(
                $"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");
        }
    }

    private static void ValidateRating(double? rating)
    {
        if (!rating.HasValue)
            return;

        var value = rating.Value;
        var isHalfStep = Math.Abs(
            value * 2 - Math.Round(value * 2)) < 0.000001;

        if (value < 0.5 || value > 5 || !isHalfStep)
        {
            throw new BadRequestException(
                "Reytinq 0.5 ilə 5 arasında və 0.5 addımlarla olmalıdır.");
        }
    }

    private static string GetStatusLabel(UserGameStatus status)
    {
        return status switch
        {
            UserGameStatus.Playing => "Oynayır",
            UserGameStatus.PlanToPlay => "Oynayacaq",
            UserGameStatus.Played => "Oynayıb",
            UserGameStatus.Dropped => "Yarıda buraxıb",
            _ => status.ToString()
        };
    }

    private static DateOnly GetTodayInAzerbaijan()
    {
        return DateOnly.FromDateTime(
            DateTime.UtcNow.AddHours(AzerbaijanUtcOffsetHours));
    }

    private sealed class ProfileGameCandidateRow
    {
        public CatalogSource Source { get; init; }
        public long GameId { get; init; }
        public UserGameStatus? Status { get; init; }
        public bool IsFavorite { get; init; }
        public bool IsWishlisted { get; init; }
        public double? Rating { get; init; }
        public long? ReviewId { get; init; }
        public DateOnly? PlayedOn { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }

    private sealed record PlaytimeReviewRow(
        CatalogSource Source,
        long GameId,
        DateOnly? PlayedOn,
        CatalogSource? PlatformSource,
        long? PlatformId,
        int PlaytimeMinutes,
        DateTime ActivityAtUtc);

    private sealed record PlaytimeRange(
        DateOnly StartDate,
        DateOnly EndDate);

    private sealed record PlaytimeGameMetadata(
        string Name,
        string? CoverUrl,
        IReadOnlyList<string> Genres);

    private sealed record LookupItem(
        CatalogSource Source,
        long Id,
        string Name);

}
