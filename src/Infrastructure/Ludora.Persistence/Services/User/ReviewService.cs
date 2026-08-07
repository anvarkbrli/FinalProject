using Ludora.Application.Common;
using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models.Notification;
using Ludora.Domain.Entities;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;

using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services
{
    public sealed class ReviewService : IReviewService
    {
        private const int MaxContentLength = 10000;
        private const int MaxPageSize = 100;
        private const int MaxPlaytimeMinutes = 600000;
        private const int AzerbaijanUtcOffsetHours = 4;

        private readonly AppDbContext _context;
        private readonly IIgdbClient _igdbClient;
        private readonly IGameService _gameService;
        private readonly INotificationService _notificationService;
        private readonly IAppCache _cache;

        public ReviewService(
            AppDbContext context,
            IIgdbClient igdbClient,
            IGameService gameService,
            INotificationService notificationService,
            IAppCache cache)
        {
            _context = context;
            _igdbClient = igdbClient;
            _gameService = gameService;
            _notificationService = notificationService;
            _cache = cache;
        }

        public async Task<ReviewDTO> CreateAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            ReviewCreateDTO dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            gameId.EnsureValidCatalogReference(source, "Oyun");

            var content = dto.Content.NormalizeOptionalText(
                "Rəy mətni",
                MaxContentLength) ?? string.Empty;
            var playedOn = dto.PlayedOn ?? GetTodayInAzerbaijan();

            ValidateRating(dto.Rating);
            ValidateStatus(dto.Status);
            ValidatePlayedOn(playedOn);
            ValidateReviewDetails(
                dto.PlatformSource,
                dto.PlatformId,
                dto.PlaytimeMinutes);
            ValidateLogHasContent(
                content,
                dto.Rating,
                dto.Status,
                dto.PlatformSource,
                dto.PlaytimeMinutes);

            var userExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(x => x.Id == userId, cancellationToken);

            if (!userExists)
                throw new NotFoundException("İstifadəçi tapılmadı.");

            var game = await _gameService.GetByIdAsync(
                source,
                gameId,
                cancellationToken);

            EnsurePlatformBelongsToGame(
                game.Platforms,
                dto.PlatformSource,
                dto.PlatformId);
            var isPcPlatform = IsPcPlatform(
                dto.PlatformSource,
                dto.PlatformId,
                game.Platforms
                    .FirstOrDefault(platform =>
                        platform.Source == dto.PlatformSource &&
                        platform.Id == dto.PlatformId)
                    ?.Name);

            var review = await _context.Reviews
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                        x.UserId == userId &&
                        x.Source == source &&
                        x.GameId == gameId,
                    cancellationToken);

            if (review is not null && !review.IsDeleted)
            {
                throw new ConflictException(
                    "Bu oyun üçün artıq log mövcuddur.");
            }

            var now = DateTime.UtcNow;

            if (review is null)
            {
                review = new Review
                {
                    UserId = userId,
                    Source = source,
                    GameId = gameId,
                    Content = content,
                    ContainsSpoilers = dto.ContainsSpoilers,
                    PlayedOn = playedOn,
                    PlatformSource = dto.PlatformSource,
                    PlatformId = dto.PlatformId,
                    PlaytimeMinutes = NormalizePlaytimeMinutes(dto.PlaytimeMinutes),
                    CreatedAt = now
                };

                await _context.Reviews.AddAsync(review, cancellationToken);
            }
            else
            {
                review.Content = content;
                review.ContainsSpoilers = dto.ContainsSpoilers;
                review.PlayedOn = playedOn;
                review.PlatformSource = dto.PlatformSource;
                review.PlatformId = dto.PlatformId;
                review.PlaytimeMinutes = NormalizePlaytimeMinutes(dto.PlaytimeMinutes);
                review.IsSteamAutoLogged = false;
                review.IsSteamPlaytimeOverridden = false;
                review.IsSteamPlayedOnOverridden = false;
                review.CreatedAt = now;
                review.UpdatedAt = null;
                review.IsDeleted = false;
                review.DeletedAtUtc = null;
            }

            await ApplyHardwareSnapshotAsync(
                review,
                userId,
                dto.ShowHardwareProfile,
                dto.HardwareGpu,
                dto.HardwareCpu,
                dto.HardwareRamGb,
                dto.HardwareVramGb,
                HasReviewContent(content),
                isPcPlatform,
                cancellationToken);

            await using var transaction = await _context.Database
                .BeginTransactionAsync(cancellationToken);

            await ApplyUserGameStateAsync(
                userId,
                source,
                gameId,
                dto.Status,
                dto.Rating,
                clearNullValues: false,
                cancellationToken);

            await SaveChangesAsync(cancellationToken);

            _context.UserActivities.AddUserActivity(
                userId,
                UserActivityType.ReviewCreated,
                gameSource: source,
                gameId: gameId,
                reviewId: review.Id,
                status: dto.Status,
                rating: dto.Rating);

            if (HasReviewContent(content))
            {
                await _notificationService.QueueForFollowersAsync(
                    new FollowerNotificationRequest
                    {
                        ActorUserId = userId,
                        Type = NotificationType.FollowingReviewCreated,
                        EntityType = NotificationEntityType.Review,
                        EntityId = review.Id.ToString(),
                        Title = "Yeni rəy",
                        Message = $"{game.Title} oyunu üçün yeni rəy yazdı.",
                        ActionUrl = $"/games/{source}/{gameId}#reviews"
                    },
                    cancellationToken);
            }

            await SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await GetMineForGameAsync(
                userId,
                source,
                gameId,
                cancellationToken)
                ?? throw new NotFoundException("Oyun logu tapılmadı.");
        }

        public async Task<ReviewDTO> GetByIdAsync(
            long reviewId,
            Guid? currentUserId,
            CancellationToken cancellationToken = default)
        {
            EnsureValidReviewId(reviewId);

            var review = await _context.Reviews
                .AsNoTracking()
                .Include(x => x.User)
                .FirstOrDefaultAsync(
                    x => x.Id == reviewId && x.Content != string.Empty,
                    cancellationToken)
                ?? throw new NotFoundException("Rəy tapılmadı.");

            await EnsureCanViewAuthorAsync(
                review.UserId,
                review.User.IsPrivate,
                currentUserId,
                cancellationToken);

            var items = await MapManyAsync(
                [review],
                currentUserId,
                cancellationToken);

            return items[0];
        }

        public async Task<PagedResultDTO<ReviewDTO>> GetGameReviewsAsync(
            CatalogSource source,
            long gameId,
            ReviewFilterDTO filter,
            Guid? currentUserId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            gameId.EnsureValidCatalogReference(source, "Oyun");
            ValidateFilter(filter);

            await EnsureGameExistsAsync(source, gameId, cancellationToken);

            var query = _context.Reviews
                .AsNoTracking()
                .Where(x =>
                    x.Source == source &&
                    x.GameId == gameId &&
                    x.Content != string.Empty);

            query = ApplyVisibility(query, currentUserId);

            return await CreatePagedResultAsync(
                query,
                filter,
                currentUserId,
                cancellationToken);
        }

        public async Task<PagedResultDTO<ReviewDTO>> GetUserReviewsAsync(
            Guid userId,
            ReviewFilterDTO filter,
            Guid? currentUserId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ValidateFilter(filter);

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

            await EnsureCanViewAuthorAsync(
                user.Id,
                user.IsPrivate,
                currentUserId,
                cancellationToken);

            var query = _context.Reviews
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId &&
                    x.Content != string.Empty);

            return await CreatePagedResultAsync(
                query,
                filter,
                currentUserId,
                cancellationToken);
        }

        public async Task<PagedResultDTO<ReviewDTO>> GetMineAsync(
            Guid userId,
            ReviewFilterDTO filter,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ValidateFilter(filter);

            var query = _context.Reviews
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId &&
                    x.Content != string.Empty);

            return await CreatePagedResultAsync(
                query,
                filter,
                userId,
                cancellationToken);
        }

        public async Task<ReviewDTO?> GetMineForGameAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            CancellationToken cancellationToken = default)
        {
            gameId.EnsureValidCatalogReference(source, "Oyun");

            var review = await _context.Reviews
                .AsNoTracking()
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                        x.UserId == userId &&
                        x.Source == source &&
                        x.GameId == gameId,
                    cancellationToken);

            if (review is null)
                return null;

            var items = await MapManyAsync(
                [review],
                userId,
                cancellationToken);

            return items[0];
        }

        public async Task<ReviewDTO> UpdateAsync(
            Guid userId,
            long reviewId,
            ReviewUpdateDTO dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            EnsureValidReviewId(reviewId);

            var content = dto.Content.NormalizeOptionalText(
                "Rəy mətni",
                MaxContentLength) ?? string.Empty;

            ValidateRating(dto.Rating);
            ValidateStatus(dto.Status);
            ValidateReviewDetails(
                dto.PlatformSource,
                dto.PlatformId,
                dto.PlaytimeMinutes);

            var review = await _context.Reviews
                .FirstOrDefaultAsync(
                    x => x.Id == reviewId,
                    cancellationToken)
                ?? throw new NotFoundException("Rəy tapılmadı.");

            if (review.UserId != userId)
            {
                throw new ForbiddenException(
                    "Bu rəyi dəyişmək icazəniz yoxdur.");
            }

            var playedOn = dto.PlayedOn ??
                           review.PlayedOn ??
                           GetTodayInAzerbaijan();
            ValidatePlayedOn(playedOn);
            ValidateLogHasContent(
                content,
                dto.Rating,
                dto.Status,
                dto.PlatformSource,
                dto.PlaytimeMinutes);

            var normalizedPlaytimeMinutes = NormalizePlaytimeMinutes(dto.PlaytimeMinutes);
            var hadReviewContent = HasReviewContent(review.Content);
            var hasReviewContent = HasReviewContent(content);

            if (review.IsSteamAutoLogged)
            {
                if (review.PlaytimeMinutes != normalizedPlaytimeMinutes)
                    review.IsSteamPlaytimeOverridden = true;

                if (review.PlayedOn != playedOn)
                    review.IsSteamPlayedOnOverridden = true;
            }

            var game = await _gameService.GetByIdAsync(
                review.Source,
                review.GameId,
                cancellationToken);

            EnsurePlatformBelongsToGame(
                game.Platforms,
                dto.PlatformSource,
                dto.PlatformId);
            var isPcPlatform = IsPcPlatform(
                dto.PlatformSource,
                dto.PlatformId,
                game.Platforms
                    .FirstOrDefault(platform =>
                        platform.Source == dto.PlatformSource &&
                        platform.Id == dto.PlatformId)
                    ?.Name);

            review.Content = content;
            review.ContainsSpoilers = dto.ContainsSpoilers;
            review.PlayedOn = playedOn;
            review.PlatformSource = dto.PlatformSource;
            review.PlatformId = dto.PlatformId;
            review.PlaytimeMinutes = normalizedPlaytimeMinutes;
            review.UpdatedAt = DateTime.UtcNow;

            await ApplyHardwareSnapshotAsync(
                review,
                userId,
                dto.ShowHardwareProfile,
                dto.HardwareGpu,
                dto.HardwareCpu,
                dto.HardwareRamGb,
                dto.HardwareVramGb,
                hasReviewContent,
                isPcPlatform,
                cancellationToken);

            await ApplyUserGameStateAsync(
                userId,
                review.Source,
                review.GameId,
                dto.Status,
                dto.Rating,
                clearNullValues: true,
                cancellationToken);

            if (hadReviewContent && !hasReviewContent)
            {
                await RemoveReviewInteractionsAsync(
                    review.Id,
                    cancellationToken);
            }

            _context.UserActivities.AddUserActivity(
                userId,
                UserActivityType.ReviewUpdated,
                gameSource: review.Source,
                gameId: review.GameId,
                reviewId: review.Id,
                status: dto.Status,
                rating: dto.Rating);

            if (!hadReviewContent && hasReviewContent)
            {
                await _notificationService.QueueForFollowersAsync(
                    new FollowerNotificationRequest
                    {
                        ActorUserId = userId,
                        Type = NotificationType.FollowingReviewCreated,
                        EntityType = NotificationEntityType.Review,
                        EntityId = review.Id.ToString(),
                        Title = "Yeni rəy",
                        Message = $"{game.Title} oyunu üçün yeni rəy yazdı.",
                        ActionUrl = $"/games/{review.Source}/{review.GameId}#reviews"
                    },
                    cancellationToken);
            }

            await SaveChangesAsync(cancellationToken);

            return await GetMineForGameAsync(
                userId,
                review.Source,
                review.GameId,
                cancellationToken)
                ?? throw new NotFoundException("Oyun logu tapılmadı.");
        }

        public async Task DeleteAsync(
            Guid userId,
            long reviewId,
            CancellationToken cancellationToken = default)
        {
            EnsureValidReviewId(reviewId);

            var review = await _context.Reviews
                .FirstOrDefaultAsync(
                    x => x.Id == reviewId,
                    cancellationToken)
                ?? throw new NotFoundException("Rəy tapılmadı.");

            if (review.UserId != userId)
            {
                throw new ForbiddenException(
                    "Bu rəyi silmək icazəniz yoxdur.");
            }

            var now = DateTime.UtcNow;
            var userGameState = await _context.UserGames
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId &&
                    x.Source == review.Source &&
                    x.GameId == review.GameId)
                .Select(x => new
                {
                    x.Status,
                    x.Rating
                })
                .FirstOrDefaultAsync(cancellationToken);

            var hasLogData =
                review.PlaytimeMinutes is > 0 ||
                review.PlatformSource.HasValue && review.PlatformId.HasValue ||
                userGameState?.Status is not null ||
                userGameState?.Rating is not null;

            if (hasLogData)
            {
                review.Content = string.Empty;
                review.ContainsSpoilers = false;
                ClearHardwareSnapshot(review);
                review.UpdatedAt = now;
            }
            else
            {
                review.IsDeleted = true;
                review.DeletedAtUtc = now;
                review.UpdatedAt = now;
            }

            await RemoveReviewInteractionsAsync(
                reviewId,
                cancellationToken);

            _context.UserActivities.AddUserActivity(
                userId,
                UserActivityType.ReviewDeleted,
                gameSource: review.Source,
                gameId: review.GameId,
                reviewId: review.Id);

            await _context.SaveChangesAsync(cancellationToken);
            await GameCatalogCache.InvalidateAsync(_cache, cancellationToken);
        }

        private async Task<PagedResultDTO<ReviewDTO>> CreatePagedResultAsync(
            IQueryable<Review> query,
            ReviewFilterDTO filter,
            Guid? currentUserId,
            CancellationToken cancellationToken)
        {
            query = ApplyRatingFilter(query, filter.Rating);

            var totalCount = await query.CountAsync(cancellationToken);
            query = ApplySorting(query, filter.SortBy);

            var reviews = await query
                .Include(x => x.User)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            var items = await MapManyAsync(
                reviews,
                currentUserId,
                cancellationToken);

            return new PagedResultDTO<ReviewDTO>
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

        private IQueryable<Review> ApplyVisibility(
            IQueryable<Review> query,
            Guid? currentUserId)
        {
            if (!currentUserId.HasValue)
                return query.Where(x => !x.User.IsPrivate);

            var userId = currentUserId.Value;

            var blockedUserIds = _context.UserBlocks
                .Where(x =>
                    x.BlockerId == userId ||
                    x.BlockedUserId == userId)
                .Select(x =>
                    x.BlockerId == userId
                        ? x.BlockedUserId
                        : x.BlockerId);

            var followedUserIds = _context.UserFollows
                .Where(x =>
                    x.FollowerId == userId &&
                    x.Status == FollowStatus.Accepted)
                .Select(x => x.FollowingId);

            return query.Where(x =>
                !blockedUserIds.Contains(x.UserId) &&
                (!x.User.IsPrivate ||
                 x.UserId == userId ||
                 followedUserIds.Contains(x.UserId)));
        }

        private IQueryable<Review> ApplyRatingFilter(
            IQueryable<Review> query,
            double? rating)
        {
            if (!rating.HasValue)
                return query;

            var selectedRating = rating.Value;

            return query.Where(review => _context.UserGames.Any(userGame =>
                userGame.UserId == review.UserId &&
                userGame.Source == review.Source &&
                userGame.GameId == review.GameId &&
                userGame.Rating == selectedRating));
        }

        private IQueryable<Review> ApplySorting(
            IQueryable<Review> query,
            ReviewSort sort)
        {
            sort.EnsureDefined("Rəy sıralaması");

            return sort switch
            {
                ReviewSort.Newest => query
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id),

                ReviewSort.Oldest => query
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Id),

                ReviewSort.HighestRated => query
                    .OrderByDescending(review => _context.UserGames.Any(userGame =>
                        userGame.UserId == review.UserId &&
                        userGame.Source == review.Source &&
                        userGame.GameId == review.GameId &&
                        userGame.Rating.HasValue))
                    .ThenByDescending(review => _context.UserGames
                        .Where(userGame =>
                            userGame.UserId == review.UserId &&
                            userGame.Source == review.Source &&
                            userGame.GameId == review.GameId)
                        .Select(userGame => userGame.Rating)
                        .FirstOrDefault())
                    .ThenByDescending(x => x.CreatedAt),

                ReviewSort.LowestRated => query
                    .OrderByDescending(review => _context.UserGames.Any(userGame =>
                        userGame.UserId == review.UserId &&
                        userGame.Source == review.Source &&
                        userGame.GameId == review.GameId &&
                        userGame.Rating.HasValue))
                    .ThenBy(review => _context.UserGames
                        .Where(userGame =>
                            userGame.UserId == review.UserId &&
                            userGame.Source == review.Source &&
                            userGame.GameId == review.GameId)
                        .Select(userGame => userGame.Rating)
                        .FirstOrDefault())
                    .ThenByDescending(x => x.CreatedAt),

                ReviewSort.MostLiked => query
                    .OrderByDescending(review => _context.ReviewLikes
                        .Count(like => like.ReviewId == review.Id))
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id),

                ReviewSort.LeastLiked => query
                    .OrderBy(review => _context.ReviewLikes
                        .Count(like => like.ReviewId == review.Id))
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id),

                ReviewSort.MostCommented => query
                    .OrderByDescending(review => _context.ReviewComments
                        .Count(comment => comment.ReviewId == review.Id))
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id),

                ReviewSort.LeastCommented => query
                    .OrderBy(review => _context.ReviewComments
                        .Count(comment => comment.ReviewId == review.Id))
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id),

                _ => throw new BadRequestException(
                    "Rəy sıralaması düzgün deyil.")
            };
        }

        private async Task<IReadOnlyList<ReviewDTO>> MapManyAsync(
            IReadOnlyList<Review> reviews,
            Guid? currentUserId,
            CancellationToken cancellationToken)
        {
            if (reviews.Count == 0)
                return [];

            var userIds = reviews
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

            var localGameIds = reviews
                .Where(x => x.Source == CatalogSource.Local)
                .Select(x => x.GameId)
                .Distinct()
                .ToList();

            var igdbGameIds = reviews
                .Where(x => x.Source == CatalogSource.Igdb)
                .Select(x => x.GameId)
                .Distinct()
                .ToList();

            var userGames = await _context.UserGames
                .AsNoTracking()
                .Where(x =>
                    userIds.Contains(x.UserId) &&
                    (x.Source == CatalogSource.Local && localGameIds.Contains(x.GameId) ||
                     x.Source == CatalogSource.Igdb && igdbGameIds.Contains(x.GameId)))
                .ToListAsync(cancellationToken);

            var userGameStates = userGames.ToDictionary(
                x => (x.UserId, x.Source, x.GameId),
                x => x);

            var reviewIds = reviews.Select(x => x.Id).ToList();

            var likeCounts = await _context.ReviewLikes
                .AsNoTracking()
                .Where(x => reviewIds.Contains(x.ReviewId))
                .GroupBy(x => x.ReviewId)
                .ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);

            var commentCounts = await _context.ReviewComments
                .AsNoTracking()
                .Where(x => reviewIds.Contains(x.ReviewId))
                .GroupBy(x => x.ReviewId)
                .ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);

            HashSet<long> likedReviewIds = [];
            if (currentUserId.HasValue)
            {
                likedReviewIds = (await _context.ReviewLikes
                    .AsNoTracking()
                    .Where(x => x.UserId == currentUserId.Value && reviewIds.Contains(x.ReviewId))
                    .Select(x => x.ReviewId)
                    .ToListAsync(cancellationToken))
                    .ToHashSet();
            }

            var gameInfos = await ResolveGameInfosAsync(
                reviews,
                cancellationToken);

            var platformInfos = await ResolvePlatformInfosAsync(
                reviews,
                cancellationToken);

            return reviews.Select(review =>
            {
                if (!gameInfos.TryGetValue(
                        (review.Source, review.GameId),
                        out var gameInfo))
                {
                    throw new NotFoundException(
                        "Rəyə aid oyun tapılmadı.");
                }

                userGameStates.TryGetValue(
                    (review.UserId, review.Source, review.GameId),
                    out var userGame);

                return new ReviewDTO
                {
                    Id = review.Id,
                    UserId = review.UserId,
                    UserName = review.User.UserName ?? string.Empty,
                    ProfileImageUrl = review.User.ProfileImageUrl,
                    Source = review.Source,
                    GameId = review.GameId,
                    GameName = gameInfo.Name,
                    GameSlug = gameInfo.Slug,
                    GameCoverImageUrl = gameInfo.CoverImageUrl,
                    Content = review.Content,
                    Rating = userGame?.Rating,
                    Status = userGame?.Status,
                    ContainsSpoilers = review.ContainsSpoilers,
                    PlayedOn = review.PlayedOn,
                    PlatformSource = review.PlatformSource,
                    PlatformId = review.PlatformId,
                    PlatformName = review.PlatformSource.HasValue &&
                                   review.PlatformId.HasValue
                        ? platformInfos.GetValueOrDefault(
                            (review.PlatformSource.Value, review.PlatformId.Value))
                        : null,
                    PlaytimeMinutes = review.PlaytimeMinutes,
                    ShowHardwareProfile = review.ShowHardwareProfile,
                    HardwareGpu = review.ShowHardwareProfile
                        ? review.HardwareGpuName
                        : null,
                    HardwareCpu = review.ShowHardwareProfile
                        ? review.HardwareCpuName
                        : null,
                    HardwareRamGb = review.ShowHardwareProfile
                        ? review.HardwareRamGb
                        : null,
                    HardwareVramGb = review.ShowHardwareProfile
                        ? review.HardwareVramGb
                        : null,
                    CreatedAt = review.CreatedAt,
                    UpdatedAt = review.UpdatedAt,
                    IsMine = currentUserId.HasValue &&
                             review.UserId == currentUserId.Value,
                    LikeCount = likeCounts.GetValueOrDefault(review.Id),
                    CommentCount = commentCounts.GetValueOrDefault(review.Id),
                    IsLikedByCurrentUser = likedReviewIds.Contains(review.Id)
                };
            }).ToList();
        }

        private async Task<Dictionary<(CatalogSource Source, long GameId), GameInfo>>
            ResolveGameInfosAsync(
                IReadOnlyList<Review> reviews,
                CancellationToken cancellationToken)
        {
            var result = new Dictionary<(CatalogSource, long), GameInfo>();

            var localIds = reviews
                .Where(x => x.Source == CatalogSource.Local)
                .Select(x => (int)x.GameId)
                .Distinct()
                .ToList();

            if (localIds.Count > 0)
            {
                var localGames = await _context.Games
                    .AsNoTracking()
                    .Where(x => localIds.Contains(x.Id))
                    .Select(x => new
                    {
                        x.Id,
                        x.Name,
                        x.Slug,
                        x.CoverImageUrl
                    })
                    .ToListAsync(cancellationToken);

                foreach (var game in localGames)
                {
                    result[(CatalogSource.Local, game.Id)] = new GameInfo(
                        game.Name,
                        game.Slug,
                        game.CoverImageUrl);
                }
            }

            var igdbIds = reviews
                .Where(x => x.Source == CatalogSource.Igdb)
                .Select(x => x.GameId)
                .Distinct()
                .ToList();

            foreach (var gameId in igdbIds)
            {
                var game = await _igdbClient.GetGameByIdAsync(
                    gameId,
                    cancellationToken);

                if (game is null)
                    continue;

                result[(CatalogSource.Igdb, gameId)] = new GameInfo(
                    game.Name,
                    null,
                    game.CoverImageUrl);
            }

            return result;
        }

        private async Task<Dictionary<(CatalogSource Source, long PlatformId), string>>
            ResolvePlatformInfosAsync(
                IReadOnlyList<Review> reviews,
                CancellationToken cancellationToken)
        {
            var result = new Dictionary<(CatalogSource, long), string>();

            var localIds = reviews
                .Where(x =>
                    x.PlatformSource == CatalogSource.Local &&
                    x.PlatformId.HasValue)
                .Select(x => (int)x.PlatformId!.Value)
                .Distinct()
                .ToList();

            if (localIds.Count > 0)
            {
                var localPlatforms = await _context.Platforms
                    .AsNoTracking()
                    .Where(x => localIds.Contains(x.Id))
                    .Select(x => new
                    {
                        x.Id,
                        x.Name
                    })
                    .ToListAsync(cancellationToken);

                foreach (var platform in localPlatforms)
                {
                    result[(CatalogSource.Local, platform.Id)] = platform.Name;
                }
            }

            var igdbIds = reviews
                .Where(x =>
                    x.PlatformSource == CatalogSource.Igdb &&
                    x.PlatformId.HasValue)
                .Select(x => x.PlatformId!.Value)
                .Distinct()
                .ToList();

            foreach (var platformId in igdbIds)
            {
                var platform = await _igdbClient.GetPlatformByIdAsync(
                    platformId,
                    cancellationToken);

                if (platform is not null)
                {
                    result[(CatalogSource.Igdb, platformId)] = platform.Name;
                }
            }

            return result;
        }

        private async Task ApplyUserGameStateAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            UserGameStatus? status,
            double? rating,
            bool clearNullValues,
            CancellationToken cancellationToken)
        {
            var userGame = await _context.UserGames
                .FirstOrDefaultAsync(x =>
                        x.UserId == userId &&
                        x.Source == source &&
                        x.GameId == gameId,
                    cancellationToken);

            if (userGame is null)
            {
                if (!status.HasValue && !rating.HasValue)
                    return;

                userGame = new UserGame
                {
                    UserId = userId,
                    Source = source,
                    GameId = gameId,
                    Status = status,
                    Rating = rating,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.UserGames.AddAsync(
                    userGame,
                    cancellationToken);
                return;
            }

            if (clearNullValues || status.HasValue)
                userGame.Status = status;

            if (clearNullValues || rating.HasValue)
                userGame.Rating = rating;

            if (IsUserGameEmpty(userGame))
            {
                _context.UserGames.Remove(userGame);
                return;
            }

            userGame.UpdatedAt = DateTime.UtcNow;
        }

        private async Task EnsureGameExistsAsync(
            CatalogSource source,
            long gameId,
            CancellationToken cancellationToken)
        {
            if (source == CatalogSource.Local)
            {
                var exists = await _context.Games
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.Id == (int)gameId,
                        cancellationToken);

                if (!exists)
                    throw new NotFoundException("Oyun tapılmadı.");

                return;
            }

            var isHidden = await _context.IgdbGameOverrides
                .AsNoTracking()
                .AnyAsync(
                    x => x.IgdbGameId == gameId && x.IsHidden,
                    cancellationToken);

            if (isHidden)
                throw new NotFoundException("Oyun tapılmadı.");

            _ = await _igdbClient.GetGameByIdAsync(
                    gameId,
                    cancellationToken)
                ?? throw new NotFoundException(
                    "IGDB oyunu tapılmadı.");
        }

        private async Task EnsureCanViewAuthorAsync(
            Guid authorId,
            bool isPrivate,
            Guid? currentUserId,
            CancellationToken cancellationToken)
        {
            if (currentUserId.HasValue)
            {
                var isBlocked = await _context.UserBlocks
                    .AnyAsync(x =>
                            x.BlockerId == currentUserId.Value && x.BlockedUserId == authorId ||
                            x.BlockerId == authorId && x.BlockedUserId == currentUserId.Value,
                        cancellationToken);

                if (isBlocked)
                {
                    throw new ForbiddenException(
                        "Bu istifadəçinin rəylərinə baxmaq mümkün deyil.");
                }
            }

            if (!isPrivate || currentUserId == authorId)
                return;

            if (!currentUserId.HasValue)
                throw new ForbiddenException("Bu hesab gizlidir.");

            var isFollowing = await _context.UserFollows
                .AnyAsync(x =>
                        x.FollowerId == currentUserId.Value &&
                        x.FollowingId == authorId &&
                        x.Status == FollowStatus.Accepted,
                    cancellationToken);

            if (!isFollowing)
                throw new ForbiddenException("Bu hesab gizlidir.");
        }

        private async Task SaveChangesAsync(
            CancellationToken cancellationToken)
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
                    "Bu oyun üçün artıq log mövcuddur.");
            }
        }

        private async Task RemoveReviewInteractionsAsync(
            long reviewId,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var likes = await _context.ReviewLikes
                .Where(x => x.ReviewId == reviewId)
                .ToListAsync(cancellationToken);
            _context.ReviewLikes.RemoveRange(likes);

            var comments = await _context.ReviewComments
                .Where(x => x.ReviewId == reviewId)
                .ToListAsync(cancellationToken);

            foreach (var comment in comments)
            {
                comment.IsDeleted = true;
                comment.DeletedAtUtc = now;
                comment.UpdatedAt = now;
            }
        }

        private static bool IsUserGameEmpty(UserGame userGame)
        {
            return userGame.Status is null &&
                   !userGame.IsFavorite &&
                   !userGame.IsWishlisted &&
                   !userGame.Rating.HasValue;
        }

        private async Task ApplyHardwareSnapshotAsync(
            Review review,
            Guid userId,
            bool showHardwareProfile,
            string? gpu,
            string? cpu,
            int? ramGb,
            int? vramGb,
            bool hasReviewContent,
            bool isPcPlatform,
            CancellationToken cancellationToken)
        {
            if (!showHardwareProfile)
            {
                ClearHardwareSnapshot(review);
                return;
            }

            if (!hasReviewContent)
            {
                throw new BadRequestException(
                    "Sistem komponentləri yalnız mətnli rəydə göstərilə bilər.");
            }

            if (!isPcPlatform)
            {
                throw new BadRequestException(
                    "Sistem komponentləri yalnız PC platforması seçildikdə göstərilə bilər.");
            }

            var normalizedGpu = gpu.NormalizeOptionalText("GPU", 150);
            var normalizedCpu = cpu.NormalizeOptionalText("CPU", 150);

            if (normalizedGpu is null)
                throw new BadRequestException("GPU boş ola bilməz.");

            if (normalizedCpu is null)
                throw new BadRequestException("CPU boş ola bilməz.");

            if (!ramGb.HasValue || ramGb is < 1 or > 512)
            {
                throw new BadRequestException(
                    "RAM 1-512 GB aralığında olmalıdır.");
            }

            if (vramGb is < 1 or > 128)
            {
                throw new BadRequestException(
                    "VRAM 1-128 GB aralığında olmalıdır.");
            }

            review.ShowHardwareProfile = true;
            review.HardwareGpuName = normalizedGpu;
            review.HardwareCpuName = normalizedCpu;
            review.HardwareRamGb = ramGb;
            review.HardwareVramGb = vramGb;

            var user = await _context.Users
                .FirstOrDefaultAsync(
                    x => x.Id == userId,
                    cancellationToken)
                ?? throw new NotFoundException("İstifadəçi tapılmadı.");

            var hardwareChanged =
                user.FpsGpuName != normalizedGpu ||
                user.FpsCpuName != normalizedCpu ||
                user.FpsRamGb != ramGb ||
                user.FpsVramGb != vramGb;

            if (hardwareChanged)
            {
                user.FpsGpuName = normalizedGpu;
                user.FpsCpuName = normalizedCpu;
                user.FpsRamGb = ramGb;
                user.FpsVramGb = vramGb;
                user.FpsHardwareUpdatedAtUtc = DateTime.UtcNow;
            }
        }

        private static void ClearHardwareSnapshot(Review review)
        {
            review.ShowHardwareProfile = false;
            review.HardwareGpuName = null;
            review.HardwareCpuName = null;
            review.HardwareRamGb = null;
            review.HardwareVramGb = null;
        }

        private static bool IsPcPlatform(
            CatalogSource? source,
            long? platformId,
            string? platformName)
        {
            if (source == CatalogSource.Igdb && platformId == 6)
                return true;

            if (string.IsNullOrWhiteSpace(platformName))
                return false;

            var normalizedName = platformName.Trim();
            return normalizedName.Equals(
                       "PC",
                       StringComparison.OrdinalIgnoreCase) ||
                   normalizedName.StartsWith(
                       "PC (",
                       StringComparison.OrdinalIgnoreCase) ||
                   normalizedName.Contains(
                       "Microsoft Windows",
                       StringComparison.OrdinalIgnoreCase) ||
                   normalizedName.Equals(
                       "Windows",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateFilter(ReviewFilterDTO filter)
        {
            filter.SortBy.EnsureDefined("Rəy sıralaması");
            ValidateRating(filter.Rating);

            if (filter.PageNumber < 1)
            {
                throw new BadRequestException(
                    "Səhifə nömrəsi 1-dən kiçik ola bilməz.");
            }

            if (filter.PageSize is < 1 or > MaxPageSize)
            {
                throw new BadRequestException(
                    $"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");
            }

            if ((long)(filter.PageNumber - 1) * filter.PageSize > int.MaxValue)
            {
                throw new BadRequestException(
                    "Səhifə parametrləri həddindən artıq böyükdür.");
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

        private static void ValidateStatus(UserGameStatus? status)
        {
            if (status.HasValue && !Enum.IsDefined(status.Value))
                throw new BadRequestException("Oyun statusu yanlışdır.");
        }

        private static void ValidateLogHasContent(
            string content,
            double? rating,
            UserGameStatus? status,
            CatalogSource? platformSource,
            int? playtimeMinutes)
        {
            if (HasReviewContent(content) ||
                rating.HasValue ||
                status.HasValue ||
                platformSource.HasValue ||
                playtimeMinutes is > 0)
            {
                return;
            }

            throw new BadRequestException(
                "Status, reytinq, platforma, oyun müddəti və ya rəy məlumatlarından ən azı biri əlavə edilməlidir.");
        }

        private static void ValidatePlayedOn(DateOnly? playedOn)
        {
            if (playedOn.HasValue &&
                playedOn.Value > GetTodayInAzerbaijan())
            {
                throw new BadRequestException(
                    "Oynanılma tarixi gələcək tarix ola bilməz.");
            }
        }

        private static void ValidateReviewDetails(
            CatalogSource? platformSource,
            long? platformId,
            int? playtimeMinutes)
        {
            if (platformSource.HasValue != platformId.HasValue)
            {
                throw new BadRequestException(
                    "Platforma mənbəyi və ID-si birlikdə göndərilməlidir.");
            }

            if (platformSource.HasValue && platformId.HasValue)
            {
                platformId.Value.EnsureValidCatalogReference(
                    platformSource.Value,
                    "Platforma");
            }

            if (playtimeMinutes is < 0 or > MaxPlaytimeMinutes)
            {
                throw new BadRequestException(
                    $"Oyun müddəti 0-{MaxPlaytimeMinutes} dəqiqə aralığında olmalıdır.");
            }
        }

        private static void EnsurePlatformBelongsToGame(
            IReadOnlyCollection<LookupDTO> platforms,
            CatalogSource? platformSource,
            long? platformId)
        {
            if (!platformSource.HasValue || !platformId.HasValue)
                return;

            var belongsToGame = platforms.Any(x =>
                x.Source == platformSource.Value &&
                x.Id == platformId.Value);

            if (!belongsToGame)
            {
                throw new BadRequestException(
                    "Seçilmiş platforma bu oyuna aid deyil.");
            }
        }

        private static int? NormalizePlaytimeMinutes(int? playtimeMinutes)
        {
            return playtimeMinutes is null or 0
                ? null
                : playtimeMinutes;
        }

        private static bool HasReviewContent(string? content)
        {
            return !string.IsNullOrWhiteSpace(content);
        }

        private static DateOnly GetTodayInAzerbaijan()
        {
            return DateOnly.FromDateTime(
                DateTime.UtcNow.AddHours(AzerbaijanUtcOffsetHours));
        }

        private static void EnsureValidReviewId(long reviewId)
        {
            if (reviewId < 1)
            {
                throw new BadRequestException(
                    "Rəy ID-si 1-dən kiçik ola bilməz.");
            }
        }

        private sealed record GameInfo(
            string Name,
            string? Slug,
            string? CoverImageUrl);
    }
}
