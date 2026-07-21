using System.Globalization;
using System.Text;
using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Game;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

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
                SortBy = "rating_desc",
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

        var uniqueTotalCount = result.Items
            .Select(x => NormalizeSearchValue(x.Title))
            .Distinct()
            .Count();

        return new SearchSection<SearchGameResultDTO>(
            items,
            uniqueTotalCount);
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
                (x.Content.Contains(query) ||
                 (x.User.UserName != null && x.User.UserName.Contains(query))));

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
                        x.UserName,
                        query,
                        exactScore: 560,
                        prefixScore: 430,
                        containsScore: 300) +
                        CalculateSecondaryScore(x.Content, query, 260) +
                        CalculateSecondaryScore(game.Title, query, 170)
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
            .Where(x =>
                x.Name.Contains(query) ||
                (x.Description != null && x.Description.Contains(query)) ||
                (x.User.UserName != null && x.User.UserName.Contains(query)));

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
                    containsScore: 410) +
                    CalculateSecondaryScore(x.Description, query, 150) +
                    CalculateSecondaryScore(x.OwnerUserName, query, 180)
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.CreatedAt)
            .Take(resultLimit)
            .ToList();

        return new SearchSection<SearchGameListResultDTO>(items, totalCount);
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
