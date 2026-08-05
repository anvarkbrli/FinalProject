using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Home;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class HomeSocialService : IHomeSocialService
{
    private readonly AppDbContext _context;
    private readonly IIgdbClient _igdbClient;
    private readonly IUserActivityService _userActivityService;

    public HomeSocialService(
        AppDbContext context,
        IIgdbClient igdbClient,
        IUserActivityService userActivityService)
    {
        _context = context;
        _igdbClient = igdbClient;
        _userActivityService = userActivityService;
    }

    public async Task<HomeSocialDashboardDTO> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, cancellationToken);

        if (!userExists)
            throw new NotFoundException("İstifadəçi tapılmadı.");

        var acceptedFriends = await _context.UserFollows
            .AsNoTracking()
            .Where(x =>
                x.FollowerId == userId &&
                x.Status == FollowStatus.Accepted)
            .Select(x => new SocialUserRow(
                x.FollowingId,
                x.Following.UserName ?? string.Empty,
                x.Following.ProfileImageUrl,
                x.Following.IsPrivate))
            .ToListAsync(cancellationToken);

        var friendIds = acceptedFriends
            .Select(x => x.Id)
            .Distinct()
            .ToArray();
        var friendsById = acceptedFriends
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First());

        var activityResult = await _userActivityService.GetFeedAsync(
            userId,
            new UserActivityFilterDTO
            {
                Category = UserActivityFeedCategory.All,
                Scope = UserActivityFeedScope.Following,
                PageNumber = 1,
                PageSize = 8
            },
            cancellationToken);

        List<UserGameRow> usefulGameRows;
        if (friendIds.Length == 0)
        {
            usefulGameRows = [];
        }
        else
        {
            usefulGameRows = await _context.UserGames
                .AsNoTracking()
                .Where(x =>
                    friendIds.Contains(x.UserId) &&
                    (x.Status != null || x.IsFavorite || x.Rating != null))
                .Select(x => new UserGameRow(
                    x.UserId,
                    x.Source,
                    x.GameId,
                    x.Status,
                    x.IsFavorite,
                    x.Rating,
                    x.UpdatedAt ?? x.CreatedAt))
                .ToListAsync(cancellationToken);
        }

        var currentGameRows = await _context.UserGames
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                (x.Status != null || x.IsFavorite || x.Rating != null))
            .Select(x => new UserGameRow(
                x.UserId,
                x.Source,
                x.GameId,
                x.Status,
                x.IsFavorite,
                x.Rating,
                x.UpdatedAt ?? x.CreatedAt))
            .ToListAsync(cancellationToken);

        List<FriendReviewRow> friendReviewRows;
        if (friendIds.Length == 0)
        {
            friendReviewRows = [];
        }
        else
        {
            friendReviewRows = await _context.Reviews
                .AsNoTracking()
                .Where(x =>
                    friendIds.Contains(x.UserId) &&
                    !x.IsDeleted &&
                    x.Content.Trim() != string.Empty)
                .OrderByDescending(x => x.CreatedAt)
                .Take(6)
                .Select(x => new FriendReviewRow(
                    x.Id,
                    x.UserId,
                    x.Source,
                    x.GameId,
                    x.Content,
                    x.ContainsSpoilers,
                    x.CreatedAt))
                .ToListAsync(cancellationToken);
        }

        var writtenReviewKeys = friendReviewRows
            .Select(x => new UserGameKey(x.UserId, x.Source, x.GameId))
            .ToHashSet();

        var playingGroups = usefulGameRows
            .Where(x => x.Status == UserGameStatus.Playing)
            .GroupBy(x => new GameKey(x.Source, x.GameId))
            .Select(group => new PlayingGroup(
                group.Key,
                group.OrderByDescending(x => x.ActivityAtUtc)
                    .Select(x => x.UserId)
                    .Distinct()
                    .ToArray(),
                group.Max(x => x.ActivityAtUtc)))
            .OrderByDescending(x => x.LastActivityAtUtc)
            .ThenByDescending(x => x.FriendIds.Length)
            .Take(4)
            .ToList();

        var ratingRows = usefulGameRows
            .Where(x =>
                x.Rating != null &&
                !writtenReviewKeys.Contains(
                    new UserGameKey(x.UserId, x.Source, x.GameId)))
            .OrderByDescending(x => x.ActivityAtUtc)
            .ThenByDescending(x => x.Rating)
            .Take(6)
            .ToList();

        var tasteCandidate = BuildTasteCandidate(
            currentGameRows,
            usefulGameRows,
            acceptedFriends);

        var suggestions = await BuildSuggestionsAsync(
            userId,
            currentGameRows,
            cancellationToken);

        var gameKeys = playingGroups
            .Select(x => x.Game)
            .Concat(ratingRows.Select(x => new GameKey(x.Source, x.GameId)))
            .Concat(friendReviewRows.Select(x => new GameKey(x.Source, x.GameId)))
            .Concat(tasteCandidate?.SharedGames ?? [])
            .Distinct()
            .ToArray();

        var games = await ResolveGamesAsync(gameKeys, cancellationToken);

        var playing = playingGroups
            .Where(x => games.ContainsKey(x.Game))
            .Select(group => new HomeFriendsPlayingGameDTO
            {
                Game = games[group.Game],
                Friends = group.FriendIds
                    .Where(friendsById.ContainsKey)
                    .Take(4)
                    .Select(id => MapUser(friendsById[id]))
                    .ToArray(),
                FriendCount = group.FriendIds.Length,
                LastActivityAtUtc = group.LastActivityAtUtc
            })
            .ToArray();

        var ratings = ratingRows
            .Select(row => new
            {
                Row = row,
                Game = new GameKey(row.Source, row.GameId)
            })
            .Where(x =>
                games.ContainsKey(x.Game) &&
                friendsById.ContainsKey(x.Row.UserId))
            .Select(x => new HomeFriendRatingDTO
            {
                User = MapUser(friendsById[x.Row.UserId]),
                Game = games[x.Game],
                Rating = x.Row.Rating!.Value,
                RatedAtUtc = x.Row.ActivityAtUtc
            })
            .ToArray();

        var ratingsByUserGame = usefulGameRows
            .Where(x => x.Rating != null)
            .GroupBy(x => new UserGameKey(x.UserId, x.Source, x.GameId))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(x => x.ActivityAtUtc)
                    .First()
                    .Rating);

        var reviews = friendReviewRows
            .Select(row => new
            {
                Row = row,
                Game = new GameKey(row.Source, row.GameId),
                UserGame = new UserGameKey(row.UserId, row.Source, row.GameId)
            })
            .Where(x =>
                games.ContainsKey(x.Game) &&
                friendsById.ContainsKey(x.Row.UserId))
            .Select(x => new HomeFriendReviewDTO
            {
                ReviewId = x.Row.ReviewId,
                User = MapUser(friendsById[x.Row.UserId]),
                Game = games[x.Game],
                Rating = ratingsByUserGame.GetValueOrDefault(x.UserGame),
                Content = x.Row.Content,
                ContainsSpoilers = x.Row.ContainsSpoilers,
                CreatedAtUtc = x.Row.CreatedAtUtc
            })
            .ToArray();

        HomeTasteMatchDTO? tasteMatch = null;
        if (tasteCandidate is not null &&
            friendsById.TryGetValue(tasteCandidate.UserId, out var tasteUser))
        {
            tasteMatch = new HomeTasteMatchDTO
            {
                User = MapUser(tasteUser),
                MatchPercent = tasteCandidate.MatchPercent,
                SharedGameCount = tasteCandidate.SharedGameCount,
                SharedFavoriteCount = tasteCandidate.SharedFavoriteCount,
                SharedGames = tasteCandidate.SharedGames
                    .Where(games.ContainsKey)
                    .Take(3)
                    .Select(key => games[key])
                    .ToArray()
            };
        }

        return new HomeSocialDashboardDTO
        {
            FollowedUserCount = friendIds.Length,
            Activities = activityResult.Items,
            FriendsPlaying = playing,
            FriendRatings = ratings,
            FriendReviews = reviews,
            TasteMatch = tasteMatch,
            FriendSuggestions = suggestions
        };
    }

    private static TasteCandidate? BuildTasteCandidate(
        IReadOnlyCollection<UserGameRow> currentRows,
        IReadOnlyCollection<UserGameRow> friendRows,
        IReadOnlyCollection<SocialUserRow> friends)
    {
        var currentGames = currentRows
            .Select(x => new GameKey(x.Source, x.GameId))
            .ToHashSet();
        var currentFavorites = currentRows
            .Where(x => x.IsFavorite)
            .Select(x => new GameKey(x.Source, x.GameId))
            .ToHashSet();

        if (currentGames.Count == 0 || friends.Count == 0)
            return null;

        TasteCandidate? best = null;

        foreach (var friend in friends)
        {
            var rows = friendRows
                .Where(x => x.UserId == friend.Id)
                .ToArray();
            var games = rows
                .Select(x => new GameKey(x.Source, x.GameId))
                .ToHashSet();

            if (games.Count == 0)
                continue;

            var favorites = rows
                .Where(x => x.IsFavorite)
                .Select(x => new GameKey(x.Source, x.GameId))
                .ToHashSet();
            var sharedGames = currentGames.Intersect(games).ToArray();

            if (sharedGames.Length == 0)
                continue;

            var sharedFavorites = currentFavorites.Intersect(favorites).ToArray();
            var allUnion = currentGames.Union(games).Count();
            var favoriteUnion = currentFavorites.Union(favorites).Count();
            var gameScore = allUnion == 0
                ? 0d
                : sharedGames.Length / (double)allUnion;
            var favoriteScore = favoriteUnion == 0
                ? gameScore
                : sharedFavorites.Length / (double)favoriteUnion;
            var percentage = (int)Math.Clamp(
                Math.Round((gameScore * 0.72d + favoriteScore * 0.28d) * 100d),
                1d,
                100d);

            var candidate = new TasteCandidate(
                friend.Id,
                percentage,
                sharedGames.Length,
                sharedFavorites.Length,
                sharedFavorites
                    .Concat(sharedGames)
                    .Distinct()
                    .Take(6)
                    .ToArray());

            if (best is null ||
                candidate.MatchPercent > best.MatchPercent ||
                candidate.MatchPercent == best.MatchPercent &&
                candidate.SharedFavoriteCount > best.SharedFavoriteCount ||
                candidate.MatchPercent == best.MatchPercent &&
                candidate.SharedFavoriteCount == best.SharedFavoriteCount &&
                candidate.SharedGameCount > best.SharedGameCount)
            {
                best = candidate;
            }
        }

        return best;
    }

    private async Task<IReadOnlyList<HomeFriendSuggestionDTO>> BuildSuggestionsAsync(
        Guid userId,
        IReadOnlyCollection<UserGameRow> currentRows,
        CancellationToken cancellationToken)
    {
        var existingTargets = await _context.UserFollows
            .AsNoTracking()
            .Where(x => x.FollowerId == userId)
            .Select(x => x.FollowingId)
            .ToListAsync(cancellationToken);

        var blockedIds = await _context.UserBlocks
            .AsNoTracking()
            .Where(x => x.BlockerId == userId || x.BlockedUserId == userId)
            .Select(x => x.BlockerId == userId ? x.BlockedUserId : x.BlockerId)
            .ToListAsync(cancellationToken);

        var excludedIds = existingTargets
            .Concat(blockedIds)
            .Append(userId)
            .Distinct()
            .ToArray();

        var candidates = await _context.Users
            .AsNoTracking()
            .Where(x =>
                !excludedIds.Contains(x.Id) &&
                !x.IsPermanentlyBanned &&
                x.AccountDeletionRequestedAtUtc == null)
            .OrderByDescending(x => x.CreatedAt)
            .Take(40)
            .Select(x => new SocialUserRow(
                x.Id,
                x.UserName ?? string.Empty,
                x.ProfileImageUrl,
                x.IsPrivate))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return [];

        var candidateIds = candidates.Select(x => x.Id).ToArray();
        var candidateGames = await _context.UserGames
            .AsNoTracking()
            .Where(x =>
                candidateIds.Contains(x.UserId) &&
                (x.Status != null || x.IsFavorite || x.Rating != null))
            .Select(x => new UserGameRow(
                x.UserId,
                x.Source,
                x.GameId,
                x.Status,
                x.IsFavorite,
                x.Rating,
                x.UpdatedAt ?? x.CreatedAt))
            .ToListAsync(cancellationToken);

        var myAcceptedFollowingIds = await _context.UserFollows
            .AsNoTracking()
            .Where(x =>
                x.FollowerId == userId &&
                x.Status == FollowStatus.Accepted)
            .Select(x => x.FollowingId)
            .ToArrayAsync(cancellationToken);

        List<CommonFollowRow> commonFollowRows;
        if (myAcceptedFollowingIds.Length == 0)
        {
            commonFollowRows = [];
        }
        else
        {
            commonFollowRows = await _context.UserFollows
                .AsNoTracking()
                .Where(x =>
                    candidateIds.Contains(x.FollowerId) &&
                    x.Status == FollowStatus.Accepted &&
                    myAcceptedFollowingIds.Contains(x.FollowingId))
                .Select(x => new CommonFollowRow(x.FollowerId, x.FollowingId))
                .ToListAsync(cancellationToken);
        }

        var currentGames = currentRows
            .Select(x => new GameKey(x.Source, x.GameId))
            .ToHashSet();
        var currentFavorites = currentRows
            .Where(x => x.IsFavorite)
            .Select(x => new GameKey(x.Source, x.GameId))
            .ToHashSet();

        return candidates
            .Select(candidate =>
            {
                var rows = candidateGames
                    .Where(x => x.UserId == candidate.Id)
                    .ToArray();
                var games = rows
                    .Select(x => new GameKey(x.Source, x.GameId))
                    .ToHashSet();
                var favorites = rows
                    .Where(x => x.IsFavorite)
                    .Select(x => new GameKey(x.Source, x.GameId))
                    .ToHashSet();
                var sharedGames = currentGames.Intersect(games).Count();
                var sharedFavorites = currentFavorites.Intersect(favorites).Count();
                var mutual = commonFollowRows.Count(x => x.FollowerId == candidate.Id);
                var score = sharedFavorites * 5 + sharedGames * 2 + mutual * 3;

                return new
                {
                    Candidate = candidate,
                    SharedGames = sharedGames,
                    SharedFavorites = sharedFavorites,
                    Mutual = mutual,
                    Score = score
                };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.SharedFavorites)
            .ThenByDescending(x => x.SharedGames)
            .Take(4)
            .Select(x => new HomeFriendSuggestionDTO
            {
                User = MapUser(x.Candidate),
                IsPrivate = x.Candidate.IsPrivate,
                MutualFollowCount = x.Mutual,
                SharedGameCount = x.SharedGames,
                SharedFavoriteCount = x.SharedFavorites
            })
            .ToArray();
    }

    private async Task<Dictionary<GameKey, HomeSocialGameDTO>> ResolveGamesAsync(
        IReadOnlyCollection<GameKey> keys,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<GameKey, HomeSocialGameDTO>();
        if (keys.Count == 0)
            return result;

        var localIds = keys
            .Where(x => x.Source == CatalogSource.Local && x.Id <= int.MaxValue)
            .Select(x => (int)x.Id)
            .Distinct()
            .ToArray();

        if (localIds.Length > 0)
        {
            var localGames = await _context.Games
                .AsNoTracking()
                .Where(x => localIds.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.CoverImageUrl
                })
                .ToListAsync(cancellationToken);

            foreach (var game in localGames)
            {
                result[new GameKey(CatalogSource.Local, game.Id)] =
                    new HomeSocialGameDTO
                    {
                        Source = CatalogSource.Local,
                        Id = game.Id,
                        Title = game.Name,
                        CoverImageUrl = game.CoverImageUrl
                    };
            }
        }

        var igdbIds = keys
            .Where(x => x.Source == CatalogSource.Igdb)
            .Select(x => x.Id)
            .Distinct()
            .ToArray();

        if (igdbIds.Length > 0)
        {
            var igdbGames = await _igdbClient.GetGamesByIdsForCardsAsync(
                igdbIds,
                cancellationToken);

            foreach (var game in igdbGames)
            {
                result[new GameKey(CatalogSource.Igdb, game.Id)] =
                    new HomeSocialGameDTO
                    {
                        Source = CatalogSource.Igdb,
                        Id = game.Id,
                        Title = game.Name,
                        CoverImageUrl = game.CoverImageUrl
                    };
            }
        }

        return result;
    }

    private static HomeSocialUserDTO MapUser(SocialUserRow user) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        ProfileImageUrl = user.ProfileImageUrl
    };

    private readonly record struct GameKey(CatalogSource Source, long Id);
    private readonly record struct UserGameKey(
        Guid UserId,
        CatalogSource Source,
        long GameId);
    private sealed record FriendReviewRow(
        long ReviewId,
        Guid UserId,
        CatalogSource Source,
        long GameId,
        string Content,
        bool ContainsSpoilers,
        DateTime CreatedAtUtc);
    private sealed record SocialUserRow(
        Guid Id,
        string UserName,
        string? ProfileImageUrl,
        bool IsPrivate);
    private sealed record UserGameRow(
        Guid UserId,
        CatalogSource Source,
        long GameId,
        UserGameStatus? Status,
        bool IsFavorite,
        double? Rating,
        DateTime ActivityAtUtc);
    private sealed record PlayingGroup(
        GameKey Game,
        Guid[] FriendIds,
        DateTime LastActivityAtUtc);
    private sealed record TasteCandidate(
        Guid UserId,
        int MatchPercent,
        int SharedGameCount,
        int SharedFavoriteCount,
        GameKey[] SharedGames);
    private sealed record CommonFollowRow(Guid FollowerId, Guid FollowingId);
}
