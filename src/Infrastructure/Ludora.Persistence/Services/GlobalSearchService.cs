using System.Globalization;
using System.Text;

using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Game;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;

using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services
{
    public sealed class GlobalSearchService : IGlobalSearchService
    {
        private const int MinQueryLength = 2;
        private const int MaxQueryLength = 100;
        private const int MinLimit = 1;
        private const int MaxLimit = 50;

        private readonly AppDbContext _context;
        private readonly IGameService _gameService;

        public GlobalSearchService(
            AppDbContext context,
            IGameService gameService)
        {
            _context = context;
            _gameService = gameService;
        }

        public async Task<GlobalSearchResultDTO> SearchAsync(
            GlobalSearchFilterDTO filter,
            Guid? currentUserId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);

            var query = filter.Query.Trim();

            if (query.Length < MinQueryLength)
            {
                throw new BadRequestException(
                    $"Axtarış mətni ən azı {MinQueryLength} simvol olmalıdır.");
            }

            if (query.Length > MaxQueryLength)
            {
                throw new BadRequestException(
                    $"Axtarış mətni {MaxQueryLength} simvoldan uzun ola bilməz.");
            }

            if (filter.Limit is < MinLimit or > MaxLimit)
            {
                throw new BadRequestException(
                    $"Nəticə limiti {MinLimit}-{MaxLimit} aralığında olmalıdır.");
            }

            var candidateLimit = Math.Min(filter.Limit * 3, 100);

            var blockedUserIds = currentUserId.HasValue
                ? _context.UserBlocks
                    .AsNoTracking()
                    .Where(x =>
                        x.BlockerId == currentUserId.Value ||
                        x.BlockedUserId == currentUserId.Value)
                    .Select(x =>
                        x.BlockerId == currentUserId.Value
                            ? x.BlockedUserId
                            : x.BlockerId)
                : null;

            var followedUserIds = currentUserId.HasValue
                ? _context.UserFollows
                    .AsNoTracking()
                    .Where(x =>
                        x.FollowerId == currentUserId.Value &&
                        x.Status == FollowStatus.Accepted)
                    .Select(x => x.FollowingId)
                : null;

            var games = await SearchGamesAsync(
                query,
                candidateLimit,
                filter.Limit,
                cancellationToken);

            var users = await SearchUsersAsync(
                query,
                candidateLimit,
                filter.Limit,
                blockedUserIds,
                cancellationToken);

            var discussions = await SearchDiscussionsAsync(
                query,
                candidateLimit,
                filter.Limit,
                currentUserId,
                blockedUserIds,
                followedUserIds,
                cancellationToken);

            var reviews = await SearchReviewsAsync(
                query,
                candidateLimit,
                filter.Limit,
                currentUserId,
                blockedUserIds,
                followedUserIds,
                cancellationToken);

            var lists = await SearchListsAsync(
                query,
                candidateLimit,
                filter.Limit,
                currentUserId,
                blockedUserIds,
                followedUserIds,
                cancellationToken);

            return new GlobalSearchResultDTO
            {
                Query = query,
                Counts = new GlobalSearchCountsDTO
                {
                    Games = games.TotalCount,
                    Users = users.TotalCount,
                    Discussions = discussions.TotalCount,
                    Reviews = reviews.TotalCount,
                    Lists = lists.TotalCount
                },
                Games = games.Items,
                Users = users.Items,
                Discussions = discussions.Items,
                Reviews = reviews.Items,
                Lists = lists.Items
            };
        }

        public async Task<PagedResultDTO<SearchGameResultDTO>> SearchGamesPageAsync(
            SearchPageFilterDTO filter,
            CancellationToken cancellationToken = default)
        {
            var query = ValidatePageFilter(filter);
            var result = await _gameService.GetFilteredGamesAsync(
                new GameFilterDTO
                {
                    SearchName = query,
                    IncludeRelatedContent = false,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize
                },
                cancellationToken);

            var items = result.Items
                .GroupBy(item => NormalizeSearchValue(item.Title))
                .Select(group => group
                    .OrderByDescending(item => item.ReleaseDate.HasValue)
                    .ThenByDescending(item => !string.IsNullOrWhiteSpace(item.CoverImageUrl))
                    .ThenByDescending(item => item.AverageRating)
                    .ThenByDescending(item => item.Source == CatalogSource.Igdb)
                    .First())
                .Select(item => new SearchGameResultDTO
                {
                    Source = item.Source,
                    Id = item.Id,
                    Title = item.Title,
                    CoverImageUrl = item.CoverImageUrl,
                    ReleaseDate = item.ReleaseDate,
                    AverageRating = item.AverageRating,
                    Score = CalculateTitleScore(
                        item.Title,
                        query,
                        exactScore: 1050,
                        prefixScore: 760,
                        containsScore: 510) + 100
                })
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.AverageRating)
                .ThenBy(item => item.Title)
                .ToList();

            return new PagedResultDTO<SearchGameResultDTO>
            {
                Items = items,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount,
                TotalPages = result.TotalPages
            };
        }

        public async Task<PagedResultDTO<SearchUserResultDTO>> SearchUsersPageAsync(
            SearchPageFilterDTO filter,
            Guid? currentUserId,
            CancellationToken cancellationToken = default)
        {
            var query = ValidatePageFilter(filter);
            var visibility = CreateVisibilityQueries(currentUserId);
            var usersQuery = _context.Users
                .AsNoTracking()
                .Where(user =>
                    user.UserName != null &&
                    (user.UserName.Contains(query) ||
                     (user.Bio != null && user.Bio.Contains(query))));

            if (visibility.BlockedUserIds is not null)
                usersQuery = usersQuery.Where(user => !visibility.BlockedUserIds.Contains(user.Id));

            var totalCount = await usersQuery.CountAsync(cancellationToken);
            var candidates = await usersQuery
                .OrderByDescending(user => user.UserName == query)
                .ThenByDescending(user => user.UserName!.StartsWith(query))
                .ThenByDescending(user => user.UserName!.Contains(query))
                .ThenByDescending(user => user.Bio != null && user.Bio.Contains(query))
                .ThenBy(user => user.UserName)
                .Skip(GetPageOffset(filter))
                .Take(filter.PageSize)
                .Select(user => new
                {
                    user.Id,
                    UserName = user.UserName!,
                    user.ProfileImageUrl,
                    user.Bio
                })
                .ToListAsync(cancellationToken);

            var items = candidates
                .Select(user => new SearchUserResultDTO
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    ProfileImageUrl = user.ProfileImageUrl,
                    Bio = user.Bio,
                    Score = CalculateTitleScore(
                        user.UserName,
                        query,
                        exactScore: 1200,
                        prefixScore: 920,
                        containsScore: 680) +
                        CalculateSecondaryScore(user.Bio, query, 180)
                })
                .ToList();

            return CreatePage(items, totalCount, filter);
        }

        public async Task<PagedResultDTO<SearchDiscussionResultDTO>> SearchDiscussionsPageAsync(
            SearchPageFilterDTO filter,
            Guid? currentUserId,
            CancellationToken cancellationToken = default)
        {
            var query = ValidatePageFilter(filter);
            var visibility = CreateVisibilityQueries(currentUserId);
            var discussionsQuery = _context.Discussions
                .AsNoTracking()
                .Where(discussion =>
                    discussion.Title.Contains(query) ||
                    discussion.Content.Contains(query));

            if (visibility.BlockedUserIds is not null)
            {
                discussionsQuery = discussionsQuery.Where(
                    discussion => !visibility.BlockedUserIds.Contains(discussion.UserId));
            }

            discussionsQuery = currentUserId.HasValue
                ? discussionsQuery.Where(discussion =>
                    !discussion.User.IsPrivate ||
                    discussion.UserId == currentUserId.Value ||
                    visibility.FollowedUserIds!.Contains(discussion.UserId))
                : discussionsQuery.Where(discussion => !discussion.User.IsPrivate);

            var totalCount = await discussionsQuery.CountAsync(cancellationToken);
            var candidates = await discussionsQuery
                .OrderByDescending(discussion => discussion.Title == query)
                .ThenByDescending(discussion => discussion.Title.StartsWith(query))
                .ThenByDescending(discussion => discussion.Title.Contains(query))
                .ThenByDescending(discussion => discussion.CreatedAt)
                .Skip(GetPageOffset(filter))
                .Take(filter.PageSize)
                .Select(discussion => new
                {
                    discussion.Id,
                    discussion.Title,
                    discussion.Slug,
                    discussion.Content,
                    CategoryName = discussion.Category.Name,
                    AuthorUserName = discussion.User.UserName ?? string.Empty,
                    discussion.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var items = candidates
                .Select(discussion => new SearchDiscussionResultDTO
                {
                    Id = discussion.Id,
                    Title = discussion.Title,
                    Slug = discussion.Slug,
                    CategoryName = discussion.CategoryName,
                    AuthorUserName = discussion.AuthorUserName,
                    CreatedAt = discussion.CreatedAt,
                    Score = CalculateTitleScore(
                        discussion.Title,
                        query,
                        exactScore: 860,
                        prefixScore: 670,
                        containsScore: 430) +
                        CalculateSecondaryScore(discussion.Content, query, 160)
                })
                .ToList();

            return CreatePage(items, totalCount, filter);
        }

        public async Task<PagedResultDTO<SearchReviewResultDTO>> SearchReviewsPageAsync(
            SearchPageFilterDTO filter,
            Guid? currentUserId,
            CancellationToken cancellationToken = default)
        {
            var query = ValidatePageFilter(filter);
            var visibility = CreateVisibilityQueries(currentUserId);
            var reviewsQuery = _context.Reviews
                .AsNoTracking()
                .Where(review =>
                    review.Content != string.Empty &&
                    review.Content.Contains(query));

            if (visibility.BlockedUserIds is not null)
                reviewsQuery = reviewsQuery.Where(review => !visibility.BlockedUserIds.Contains(review.UserId));

            reviewsQuery = currentUserId.HasValue
                ? reviewsQuery.Where(review =>
                    !review.User.IsPrivate ||
                    review.UserId == currentUserId.Value ||
                    visibility.FollowedUserIds!.Contains(review.UserId))
                : reviewsQuery.Where(review => !review.User.IsPrivate);

            var totalCount = await reviewsQuery.CountAsync(cancellationToken);
            var candidates = await reviewsQuery
                .OrderByDescending(review => review.Content == query)
                .ThenByDescending(review => review.Content.StartsWith(query))
                .ThenByDescending(review => review.CreatedAt)
                .Skip(GetPageOffset(filter))
                .Take(filter.PageSize)
                .Select(review => new ReviewCandidate
                {
                    Id = review.Id,
                    UserId = review.UserId,
                    UserName = review.User.UserName ?? string.Empty,
                    ProfileImageUrl = review.User.ProfileImageUrl,
                    Source = review.Source,
                    GameId = review.GameId,
                    Content = review.Content,
                    ContainsSpoilers = review.ContainsSpoilers,
                    CreatedAt = review.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var references = candidates
                .Select(candidate => new CatalogReferenceDTO
                {
                    Source = candidate.Source,
                    Id = candidate.GameId
                })
                .DistinctBy(reference => new { reference.Source, reference.Id })
                .ToList();

            var games = references.Count == 0
                ? new Dictionary<(CatalogSource Source, long Id), GameDetailDTO>()
                : (await _gameService.GetByReferencesAsync(references, cancellationToken))
                    .ToDictionary(game => game.Key, game => game.Value);

            var items = candidates
                .Where(candidate => games.ContainsKey((candidate.Source, candidate.GameId)))
                .Select(candidate =>
                {
                    var game = games[(candidate.Source, candidate.GameId)];
                    var isMine = currentUserId.HasValue && currentUserId.Value == candidate.UserId;

                    return new SearchReviewResultDTO
                    {
                        Id = candidate.Id,
                        UserId = candidate.UserId,
                        UserName = candidate.UserName,
                        ProfileImageUrl = candidate.ProfileImageUrl,
                        Source = candidate.Source,
                        GameId = candidate.GameId,
                        GameName = game.Title,
                        GameCoverImageUrl = game.CoverImageUrl,
                        ContentPreview = candidate.ContainsSpoilers && !isMine
                            ? "Spoiler olan rəy"
                            : CreatePreview(candidate.Content, 180),
                        ContainsSpoilers = candidate.ContainsSpoilers,
                        IsMine = isMine,
                        CreatedAt = candidate.CreatedAt,
                        Score = CalculateTitleScore(
                            candidate.Content,
                            query,
                            exactScore: 820,
                            prefixScore: 640,
                            containsScore: 410)
                    };
                })
                .ToList();

            return CreatePage(items, totalCount, filter);
        }

        public async Task<PagedResultDTO<SearchGameListResultDTO>> SearchListsPageAsync(
            SearchPageFilterDTO filter,
            Guid? currentUserId,
            CancellationToken cancellationToken = default)
        {
            var query = ValidatePageFilter(filter);
            var visibility = CreateVisibilityQueries(currentUserId);
            var listsQuery = _context.GameLists
                .AsNoTracking()
                .Where(list => list.Visibility != ListVisibility.Private)
                .Where(list => list.Name.Contains(query));

            if (visibility.BlockedUserIds is not null)
                listsQuery = listsQuery.Where(list => !visibility.BlockedUserIds.Contains(list.UserId));

            listsQuery = currentUserId.HasValue
                ? listsQuery.Where(list =>
                    list.UserId == currentUserId.Value ||
                    list.Visibility == ListVisibility.Public ||
                    (list.Visibility == ListVisibility.FollowersOnly &&
                     visibility.FollowedUserIds!.Contains(list.UserId)))
                : listsQuery.Where(list => list.Visibility == ListVisibility.Public);

            var totalCount = await listsQuery.CountAsync(cancellationToken);
            var candidates = await listsQuery
                .OrderByDescending(list => list.Name == query)
                .ThenByDescending(list => list.Name.StartsWith(query))
                .ThenByDescending(list => list.CreatedAt)
                .Skip(GetPageOffset(filter))
                .Take(filter.PageSize)
                .Select(list => new
                {
                    list.Id,
                    list.Name,
                    list.Description,
                    list.Visibility,
                    list.IsRanked,
                    OwnerId = list.UserId,
                    OwnerUserName = list.User.UserName ?? string.Empty,
                    GameCount = list.GameListItems.Count,
                    list.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var items = candidates
                .Select(list => new SearchGameListResultDTO
                {
                    Id = list.Id,
                    Name = list.Name,
                    Description = list.Description,
                    Visibility = list.Visibility,
                    IsRanked = list.IsRanked,
                    OwnerId = list.OwnerId,
                    OwnerUserName = list.OwnerUserName,
                    GameCount = list.GameCount,
                    CreatedAt = list.CreatedAt,
                    Score = CalculateTitleScore(
                        list.Name,
                        query,
                        exactScore: 820,
                        prefixScore: 640,
                        containsScore: 410)
                })
                .ToList();

            return CreatePage(items, totalCount, filter);
        }

        private async Task<SearchSection<SearchGameResultDTO>> SearchGamesAsync(
            string query,
            int candidateLimit,
            int resultLimit,
            CancellationToken cancellationToken)
        {
            var result = await _gameService.GetFilteredGamesAsync(
                new GameFilterDTO
                {
                    SearchName = query,
                    IncludeRelatedContent = false,
                    PageNumber = 1,
                    PageSize = candidateLimit
                },
                cancellationToken);

            var items = result.Items
                .GroupBy(x => NormalizeSearchValue(x.Title))
                .Select(group => group
                    .OrderByDescending(x => x.ReleaseDate.HasValue)
                    .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.CoverImageUrl))
                    .ThenByDescending(x => x.AverageRating)
                    .ThenByDescending(x => x.Source == CatalogSource.Igdb)
                    .First())
                .Select(x => new SearchGameResultDTO
                {
                    Source = x.Source,
                    Id = x.Id,
                    Title = x.Title,
                    CoverImageUrl = x.CoverImageUrl,
                    ReleaseDate = x.ReleaseDate,
                    AverageRating = x.AverageRating,
                    Score = CalculateTitleScore(
                        x.Title,
                        query,
                        exactScore: 1050,
                        prefixScore: 760,
                        containsScore: 510) + 100
                })
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.AverageRating)
                .ThenBy(x => x.Title)
                .Take(resultLimit)
                .ToList();

            return new SearchSection<SearchGameResultDTO>(
                items,
                result.TotalCount);
        }

        private async Task<SearchSection<SearchUserResultDTO>> SearchUsersAsync(
            string query,
            int candidateLimit,
            int resultLimit,
            IQueryable<Guid>? blockedUserIds,
            CancellationToken cancellationToken)
        {
            var usersQuery = _context.Users
                .AsNoTracking()
                .Where(x =>
                    x.UserName != null &&
                    (x.UserName.Contains(query) ||
                     (x.Bio != null && x.Bio.Contains(query))));

            if (blockedUserIds is not null)
                usersQuery = usersQuery.Where(x => !blockedUserIds.Contains(x.Id));

            var totalCount = await usersQuery.CountAsync(cancellationToken);

            var candidates = await usersQuery
                .OrderBy(x => x.UserName)
                .Take(candidateLimit)
                .Select(x => new
                {
                    x.Id,
                    UserName = x.UserName!,
                    x.ProfileImageUrl,
                    x.Bio
                })
                .ToListAsync(cancellationToken);

            var items = candidates
                .Select(x => new SearchUserResultDTO
                {
                    Id = x.Id,
                    UserName = x.UserName,
                    ProfileImageUrl = x.ProfileImageUrl,
                    Bio = x.Bio,
                    Score = CalculateTitleScore(
                        x.UserName,
                        query,
                        exactScore: 1200,
                        prefixScore: 920,
                        containsScore: 680) +
                        CalculateSecondaryScore(x.Bio, query, 180)
                })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.UserName)
                .Take(resultLimit)
                .ToList();

            return new SearchSection<SearchUserResultDTO>(items, totalCount);
        }

        private async Task<SearchSection<SearchDiscussionResultDTO>> SearchDiscussionsAsync(
            string query,
            int candidateLimit,
            int resultLimit,
            Guid? currentUserId,
            IQueryable<Guid>? blockedUserIds,
            IQueryable<Guid>? followedUserIds,
            CancellationToken cancellationToken)
        {
            var discussionsQuery = _context.Discussions
                .AsNoTracking()
                .Where(x =>
                    x.Title.Contains(query) ||
                    x.Content.Contains(query));

            if (blockedUserIds is not null)
                discussionsQuery = discussionsQuery.Where(x => !blockedUserIds.Contains(x.UserId));

            discussionsQuery = currentUserId.HasValue
                ? discussionsQuery.Where(x =>
                    !x.User.IsPrivate ||
                    x.UserId == currentUserId.Value ||
                    followedUserIds!.Contains(x.UserId))
                : discussionsQuery.Where(x => !x.User.IsPrivate);

            var totalCount = await discussionsQuery.CountAsync(cancellationToken);

            var candidates = await discussionsQuery
                .OrderByDescending(x => x.CreatedAt)
                .Take(candidateLimit)
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.Slug,
                    x.Content,
                    CategoryName = x.Category.Name,
                    AuthorUserName = x.User.UserName ?? string.Empty,
                    x.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var items = candidates
                .Select(x => new SearchDiscussionResultDTO
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    CategoryName = x.CategoryName,
                    AuthorUserName = x.AuthorUserName,
                    CreatedAt = x.CreatedAt,
                    Score = CalculateTitleScore(
                        x.Title,
                        query,
                        exactScore: 860,
                        prefixScore: 670,
                        containsScore: 430) +
                        CalculateSecondaryScore(x.Content, query, 160)
                })
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.CreatedAt)
                .Take(resultLimit)
                .ToList();

            return new SearchSection<SearchDiscussionResultDTO>(items, totalCount);
        }

        private async Task<SearchSection<SearchReviewResultDTO>> SearchReviewsAsync(
            string query,
            int candidateLimit,
            int resultLimit,
            Guid? currentUserId,
            IQueryable<Guid>? blockedUserIds,
            IQueryable<Guid>? followedUserIds,
            CancellationToken cancellationToken)
        {
            var reviewsQuery = _context.Reviews
                .AsNoTracking()
                .Where(x =>
                    x.Content != string.Empty &&
                    x.Content.Contains(query));

            if (blockedUserIds is not null)
                reviewsQuery = reviewsQuery.Where(x => !blockedUserIds.Contains(x.UserId));

            reviewsQuery = currentUserId.HasValue
                ? reviewsQuery.Where(x =>
                    !x.User.IsPrivate ||
                    x.UserId == currentUserId.Value ||
                    followedUserIds!.Contains(x.UserId))
                : reviewsQuery.Where(x => !x.User.IsPrivate);

            var totalCount = await reviewsQuery.CountAsync(cancellationToken);

            var candidates = await reviewsQuery
                .OrderByDescending(x => x.CreatedAt)
                .Take(candidateLimit)
                .Select(x => new ReviewCandidate
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    UserName = x.User.UserName ?? string.Empty,
                    ProfileImageUrl = x.User.ProfileImageUrl,
                    Source = x.Source,
                    GameId = x.GameId,
                    Content = x.Content,
                    ContainsSpoilers = x.ContainsSpoilers,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var references = candidates
                .Select(x => new CatalogReferenceDTO
                {
                    Source = x.Source,
                    Id = x.GameId
                })
                .DistinctBy(x => new { x.Source, x.Id })
                .ToList();

            var games = references.Count == 0
                ? new Dictionary<(CatalogSource Source, long Id), GameDetailDTO>()
                : (await _gameService.GetByReferencesAsync(
                        references,
                        cancellationToken))
                    .ToDictionary(x => x.Key, x => x.Value);

            var items = candidates
                .Where(x => games.ContainsKey((x.Source, x.GameId)))
                .Select(x =>
                {
                    var game = games[(x.Source, x.GameId)];
                    var isMine = currentUserId.HasValue && currentUserId.Value == x.UserId;

                    return new SearchReviewResultDTO
                    {
                        Id = x.Id,
                        UserId = x.UserId,
                        UserName = x.UserName,
                        ProfileImageUrl = x.ProfileImageUrl,
                        Source = x.Source,
                        GameId = x.GameId,
                        GameName = game.Title,
                        GameCoverImageUrl = game.CoverImageUrl,
                        ContentPreview = x.ContainsSpoilers && !isMine
                            ? "Spoiler olan rəy"
                            : CreatePreview(x.Content, 180),
                        ContainsSpoilers = x.ContainsSpoilers,
                        IsMine = isMine,
                        CreatedAt = x.CreatedAt,
                        Score = CalculateTitleScore(
                            x.Content,
                            query,
                            exactScore: 820,
                            prefixScore: 640,
                            containsScore: 410)
                    };
                })
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.CreatedAt)
                .Take(resultLimit)
                .ToList();

            return new SearchSection<SearchReviewResultDTO>(items, totalCount);
        }

        private async Task<SearchSection<SearchGameListResultDTO>> SearchListsAsync(
            string query,
            int candidateLimit,
            int resultLimit,
            Guid? currentUserId,
            IQueryable<Guid>? blockedUserIds,
            IQueryable<Guid>? followedUserIds,
            CancellationToken cancellationToken)
        {
            var listsQuery = _context.GameLists
                .AsNoTracking()
                .Where(x => x.Visibility != ListVisibility.Private)
                .Where(x => x.Name.Contains(query));

            if (blockedUserIds is not null)
                listsQuery = listsQuery.Where(x => !blockedUserIds.Contains(x.UserId));

            listsQuery = currentUserId.HasValue
                ? listsQuery.Where(x =>
                    x.UserId == currentUserId.Value ||
                    x.Visibility == ListVisibility.Public ||
                    (x.Visibility == ListVisibility.FollowersOnly &&
                     followedUserIds!.Contains(x.UserId)))
                : listsQuery.Where(x => x.Visibility == ListVisibility.Public);

            var totalCount = await listsQuery.CountAsync(cancellationToken);

            var candidates = await listsQuery
                .OrderByDescending(x => x.CreatedAt)
                .Take(candidateLimit)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Description,
                    x.Visibility,
                    x.IsRanked,
                    OwnerId = x.UserId,
                    OwnerUserName = x.User.UserName ?? string.Empty,
                    GameCount = x.GameListItems.Count,
                    x.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var items = candidates
                .Select(x => new SearchGameListResultDTO
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    Visibility = x.Visibility,
                    IsRanked = x.IsRanked,
                    OwnerId = x.OwnerId,
                    OwnerUserName = x.OwnerUserName,
                    GameCount = x.GameCount,
                    CreatedAt = x.CreatedAt,
                    Score = CalculateTitleScore(
                        x.Name,
                        query,
                        exactScore: 820,
                        prefixScore: 640,
                        containsScore: 410)
                })
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.CreatedAt)
                .Take(resultLimit)
                .ToList();

            return new SearchSection<SearchGameListResultDTO>(items, totalCount);
        }

        private string ValidatePageFilter(SearchPageFilterDTO filter)
        {
            ArgumentNullException.ThrowIfNull(filter);
            var query = filter.Query.Trim();

            if (query.Length < MinQueryLength)
                throw new BadRequestException($"Axtarış mətni ən azı {MinQueryLength} simvol olmalıdır.");
            if (query.Length > MaxQueryLength)
                throw new BadRequestException($"Axtarış mətni {MaxQueryLength} simvoldan uzun ola bilməz.");
            if (filter.PageNumber is < 1 or > 50)
                throw new BadRequestException("Səhifə nömrəsi 1-50 aralığında olmalıdır.");
            if (filter.PageSize is < 1 or > MaxLimit)
                throw new BadRequestException($"Səhifə ölçüsü 1-{MaxLimit} aralığında olmalıdır.");

            return query;
        }

        private (IQueryable<Guid>? BlockedUserIds, IQueryable<Guid>? FollowedUserIds)
            CreateVisibilityQueries(Guid? currentUserId)
        {
            if (!currentUserId.HasValue)
                return (null, null);

            var blockedUserIds = _context.UserBlocks
                .AsNoTracking()
                .Where(item =>
                    item.BlockerId == currentUserId.Value ||
                    item.BlockedUserId == currentUserId.Value)
                .Select(item =>
                    item.BlockerId == currentUserId.Value
                        ? item.BlockedUserId
                        : item.BlockerId);

            var followedUserIds = _context.UserFollows
                .AsNoTracking()
                .Where(item =>
                    item.FollowerId == currentUserId.Value &&
                    item.Status == FollowStatus.Accepted)
                .Select(item => item.FollowingId);

            return (blockedUserIds, followedUserIds);
        }

        private static int GetPageOffset(SearchPageFilterDTO filter) =>
            checked((filter.PageNumber - 1) * filter.PageSize);

        private static PagedResultDTO<T> CreatePage<T>(
            IReadOnlyList<T> items,
            int totalCount,
            SearchPageFilterDTO filter)
        {
            var totalPages = totalCount == 0
                ? 0
                : Math.Min(
                    50,
                    (int)Math.Ceiling(totalCount / (double)filter.PageSize));

            return new PagedResultDTO<T>
            {
                Items = items,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };
        }

        private static int CalculateTitleScore(
            string? value,
            string query,
            int exactScore,
            int prefixScore,
            int containsScore)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            var normalizedValue = NormalizeSearchValue(value);
            var normalizedQuery = NormalizeSearchValue(query);

            if (normalizedValue == normalizedQuery)
                return exactScore;

            if (normalizedValue.StartsWith(normalizedQuery, StringComparison.Ordinal))
                return prefixScore;

            return normalizedValue.Contains(normalizedQuery, StringComparison.Ordinal)
                ? containsScore
                : 0;
        }

        private static int CalculateSecondaryScore(
            string? value,
            string query,
            int containsScore)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            return NormalizeSearchValue(value)
                .Contains(NormalizeSearchValue(query), StringComparison.Ordinal)
                    ? containsScore
                    : 0;
        }

        private static string NormalizeSearchValue(string value)
        {
            var normalized = value
                .Normalize(NormalizationForm.FormD)
                .ToLower(CultureInfo.GetCultureInfo("az-AZ"));

            var builder = new StringBuilder(normalized.Length);

            foreach (var character in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);

                if (category == UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsLetterOrDigit(character))
                    builder.Append(character);
            }

            return builder.ToString();
        }

        private static string CreatePreview(string value, int maxLength)
        {
            var normalized = value.Trim();

            return normalized.Length <= maxLength
                ? normalized
                : $"{normalized[..maxLength].TrimEnd()}…";
        }

        private sealed record SearchSection<T>(
            IReadOnlyList<T> Items,
            int TotalCount);

        private sealed record ReviewCandidate
        {
            public long Id { get; init; }
            public Guid UserId { get; init; }
            public string UserName { get; init; } = string.Empty;
            public string? ProfileImageUrl { get; init; }
            public CatalogSource Source { get; init; }
            public long GameId { get; init; }
            public string Content { get; init; } = string.Empty;
            public bool ContainsSpoilers { get; init; }
            public DateTime CreatedAt { get; init; }
        }
    }
}
