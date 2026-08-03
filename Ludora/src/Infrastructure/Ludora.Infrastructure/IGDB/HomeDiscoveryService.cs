using System.Text;
using Ludora.Application.DTOs;
using Ludora.Application.DTOs.GameList;
using Ludora.Application.DTOs.Home;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models;
using Ludora.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Ludora.Infrastructure.IGDB;

public sealed class HomeDiscoveryService : IHomeDiscoveryService
{
    private const string FreshCacheKey = "home-discovery:v6:fresh";
    private const string StaleCacheKey = "home-discovery:v6:stale";
    private const int SectionSize = 18;
    private const int ComingSoonSectionSize = 18;
    private const int PopularityCandidateCount = 80;
    private const int UpcomingCandidateCount = 200;
    private const int UpcomingWindowMonths = 18;
    private const int AzerbaijanUtcOffsetHours = 4;

    private static readonly SemaphoreSlim CacheLock = new(1, 1);

    private static readonly GameType[] MainCatalogGameTypes =
    [
        GameType.MainGame,
        GameType.StandaloneExpansion,
        GameType.Remake,
        GameType.Remaster,
        GameType.ExpandedGame,
        GameType.Port
    ];

    private static readonly (string Name, double Weight)[] TrendingMetrics =
    [
        ("24hr Peak Players", 0.30),
        ("Playing", 0.25),
        ("Visits", 0.20),
        ("Global Top Sellers", 0.15),
        ("Hours Watched", 0.10)
    ];

    private readonly IIgdbClient _igdbClient;
    private readonly IGameService _gameService;
    private readonly IAppCache _cache;
    private readonly ILogger<HomeDiscoveryService> _logger;

    public HomeDiscoveryService(
        IIgdbClient igdbClient,
        IGameService gameService,
        IAppCache cache,
        ILogger<HomeDiscoveryService> logger)
    {
        _igdbClient = igdbClient;
        _gameService = gameService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<HomeDiscoveryDTO> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var cachedResult = await _cache.GetAsync<HomeDiscoveryDTO>(
            FreshCacheKey,
            cancellationToken);
        if (cachedResult is not null)
            return cachedResult;

        await CacheLock.WaitAsync(cancellationToken);

        try
        {
            cachedResult = await _cache.GetAsync<HomeDiscoveryDTO>(
                FreshCacheKey,
                cancellationToken);
            if (cachedResult is not null)
                return cachedResult;

            var staleResult = await _cache.GetAsync<HomeDiscoveryDTO>(
                StaleCacheKey,
                cancellationToken);

            var result = await LoadDiscoveryAsync(
                staleResult,
                cancellationToken);

            await _cache.SetAsync(
                FreshCacheKey,
                result,
                TimeSpan.FromMinutes(30),
                cancellationToken);

            await _cache.SetAsync(
                StaleCacheKey,
                result,
                TimeSpan.FromHours(24),
                cancellationToken);

            return result;
        }
        finally
        {
            CacheLock.Release();
        }
    }

    private async Task<HomeDiscoveryDTO> LoadDiscoveryAsync(
        HomeDiscoveryDTO? staleResult,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<GameListItemDTO> trendingGames = [];
        IReadOnlyList<HomeComingSoonGameDTO> comingSoonGames = [];

        if (_igdbClient.IsEnabled)
        {
            try
            {
                var popularityTypes = await _igdbClient
                    .GetPopularityTypesAsync(cancellationToken);
                var trendingTask = LoadTrendingFromIgdbAsync(
                    popularityTypes,
                    cancellationToken);
                var comingSoonTask = LoadMostAnticipatedUpcomingAsync(
                    popularityTypes,
                    cancellationToken);

                await Task.WhenAll(trendingTask, comingSoonTask);

                trendingGames = await trendingTask;
                comingSoonGames = await comingSoonTask;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Ana səhifə üçün IGDB discovery məlumatları alına bilmədi; son uğurlu keş istifadə olunur.");

                trendingGames = staleResult?.TrendingGames ?? [];
                comingSoonGames = staleResult?.ComingSoonGames ?? [];
            }
        }
        else
        {
            trendingGames = staleResult?.TrendingGames ?? [];
            comingSoonGames = staleResult?.ComingSoonGames ?? [];
        }

        return new HomeDiscoveryDTO
        {
            TrendingGames = trendingGames,
            ComingSoonGames = comingSoonGames,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private async Task<IReadOnlyList<GameListItemDTO>> LoadTrendingFromIgdbAsync(
        IReadOnlyList<IgdbPopularityTypeModel> popularityTypes,
        CancellationToken cancellationToken)
    {
        var today = GetAzerbaijanToday();

        var trendingScores = await BuildTrendingScoresAsync(
            popularityTypes,
            cancellationToken);

        var trendingIds = trendingScores
            .OrderByDescending(x => x.Value)
            .Select(x => x.Key)
            .ToArray();

        if (trendingIds.Length == 0)
            return await LoadTrendingFallbackAsync(cancellationToken);

        var references = trendingIds
            .Select(x => new CatalogReferenceDTO
            {
                Source = CatalogSource.Igdb,
                Id = x
            })
            .ToArray();

        var gamesByReference = await _gameService.GetByReferencesAsync(
            references,
            cancellationToken);

        var trendingGames = trendingIds
            .Select(id => gamesByReference.GetValueOrDefault(
                (CatalogSource.Igdb, id)))
            .Where(x => x is not null)
            .Select(x => x!)
            .Where(IsDisplayableGame)
            .Where(x => x.ReleaseDate.HasValue && x.ReleaseDate.Value <= today)
            .Take(SectionSize)
            .Select(MapGame)
            .ToList();

        return trendingGames.Count > 0
            ? trendingGames
            : await LoadTrendingFallbackAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<GameListItemDTO>>
        LoadTrendingFallbackAsync(CancellationToken cancellationToken)
    {
        var today = GetAzerbaijanToday();
        var games = await _igdbClient.GetGamesAsync(
            new GameFilterDTO
            {
                Source = CatalogSource.Igdb,
                ReleasedOnly = true,
                SortBy = "rating_desc",
                PageNumber = 1,
                PageSize = SectionSize * 2,
                IncludeRelatedContent = false
            },
            cancellationToken);

        return games
            .Where(IsDisplayableGame)
            .Where(game =>
                game.ReleaseDate.HasValue &&
                game.ReleaseDate.Value <= today)
            .Take(SectionSize)
            .Select(MapGame)
            .ToArray();
    }

    private async Task<IReadOnlyList<HomeComingSoonGameDTO>>
        LoadMostAnticipatedUpcomingAsync(
            IReadOnlyList<IgdbPopularityTypeModel> popularityTypes,
            CancellationToken cancellationToken)
    {
        var today = GetAzerbaijanToday();
        var maximumReleaseDate = today.AddMonths(UpcomingWindowMonths);

        var scores = new Dictionary<long, double>();
        var wishlistedTask = LoadPopularityMetricAsync(
            popularityTypes,
            "Most Wishlisted Upcoming",
            UpcomingCandidateCount,
            cancellationToken);
        var wantToPlayTask = LoadPopularityMetricAsync(
            popularityTypes,
            "Want to Play",
            UpcomingCandidateCount,
            cancellationToken);
        var upcomingGamesTask = _igdbClient.GetUpcomingGamesAsync(
            today,
            maximumReleaseDate,
            UpcomingCandidateCount,
            cancellationToken);

        await Task.WhenAll(
            wishlistedTask,
            wantToPlayTask,
            upcomingGamesTask);

        AddNormalizedScores(
            scores,
            await wishlistedTask,
            0.50);
        AddNormalizedScores(
            scores,
            await wantToPlayTask,
            0.30);

        var hypeOrderedGames = await upcomingGamesTask;

        AddRankScores(
            scores,
            hypeOrderedGames.Select(x => x.Id),
            0.20);

        var candidateIds = scores
            .OrderByDescending(x => x.Value)
            .Select(x => x.Key)
            .Take(UpcomingCandidateCount * 2)
            .ToArray();

        if (candidateIds.Length == 0)
            return [];

        var references = candidateIds
            .Select(id => new CatalogReferenceDTO
            {
                Source = CatalogSource.Igdb,
                Id = id
            })
            .ToArray();

        var gamesByReference = await _gameService.GetByReferencesAsync(
            references,
            cancellationToken);

        var releaseWindowDays = Math.Max(
            1,
            maximumReleaseDate.DayNumber - today.DayNumber);

        var rankedGames = candidateIds
            .Select(id => gamesByReference.GetValueOrDefault(
                (CatalogSource.Igdb, id)))
            .Where(x => x is not null)
            .Select(x => x!)
            .Where(IsDisplayableGame)
            .Where(x =>
                x.ReleaseDate.HasValue &&
                x.ReleaseDate.Value >= today &&
                x.ReleaseDate.Value <= maximumReleaseDate)
            .Select(game =>
            {
                var daysUntilRelease = Math.Clamp(
                    game.ReleaseDate!.Value.DayNumber - today.DayNumber,
                    0,
                    releaseWindowDays);

                var proximityScore =
                    1d - (double)daysUntilRelease / releaseWindowDays;

                return new
                {
                    Game = game,
                    Score = scores.GetValueOrDefault(game.Id) +
                            proximityScore * 0.08
                };
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Game.ReleaseDate)
            .ThenBy(x => x.Game.Title)
            .Take(ComingSoonSectionSize)
            .ToList();

        return rankedGames
            .Select((item, index) => new HomeComingSoonGameDTO
            {
                AnticipationRank = index + 1,
                Source = item.Game.Source,
                Id = item.Game.Id,
                Title = item.Game.Title,
                CoverImageUrl = item.Game.CoverImageUrl,
                ReleaseDate = item.Game.ReleaseDate
            })
            .ToList();
    }

    private async Task<IReadOnlyList<IgdbPopularityPrimitiveModel>>
        LoadPopularityMetricAsync(
        IReadOnlyList<IgdbPopularityTypeModel> popularityTypes,
        string metricName,
        int limit,
        CancellationToken cancellationToken)
    {
        var popularityType = FindPopularityType(
            popularityTypes,
            metricName);

        if (popularityType is null)
            return [];

        return await _igdbClient.GetPopularityPrimitivesAsync(
            popularityType.Id,
            limit,
            cancellationToken);
    }

    private static void AddRankScores(
        IDictionary<long, double> scores,
        IEnumerable<long> orderedGameIds,
        double weight)
    {
        var ids = orderedGameIds
            .Where(x => x > 0)
            .Distinct()
            .Take(UpcomingCandidateCount)
            .ToArray();

        if (ids.Length == 0)
            return;

        for (var index = 0; index < ids.Length; index++)
        {
            var normalizedRank = 1d - (double)index / ids.Length;
            var weightedValue = normalizedRank * weight;

            scores.TryGetValue(ids[index], out var currentScore);
            scores[ids[index]] = currentScore + weightedValue;
        }
    }

    private async Task<Dictionary<long, double>> BuildTrendingScoresAsync(
        IReadOnlyList<IgdbPopularityTypeModel> popularityTypes,
        CancellationToken cancellationToken)
    {
        var scores = new Dictionary<long, double>();
        var metricTasks = TrendingMetrics
            .Select(async metric => new
            {
                metric.Weight,
                Primitives = await LoadPopularityMetricAsync(
                    popularityTypes,
                    metric.Name,
                    PopularityCandidateCount,
                    cancellationToken)
            })
            .ToArray();
        var metrics = await Task.WhenAll(metricTasks);

        foreach (var metric in metrics)
            AddNormalizedScores(scores, metric.Primitives, metric.Weight);

        return scores
            .OrderByDescending(x => x.Value)
            .Take(PopularityCandidateCount)
            .ToDictionary(x => x.Key, x => x.Value);
    }

    private static void AddNormalizedScores(
        IDictionary<long, double> scores,
        IReadOnlyList<IgdbPopularityPrimitiveModel> primitives,
        double weight)
    {
        var validPrimitives = primitives
            .Where(x => x.GameId > 0 && x.Value > 0)
            .ToList();

        if (validPrimitives.Count == 0)
            return;

        var maximumValue = validPrimitives.Max(x => x.Value);

        if (maximumValue <= 0)
            return;

        foreach (var primitive in validPrimitives)
        {
            var normalizedValue = (double)(primitive.Value / maximumValue);
            var weightedValue = normalizedValue * weight;

            scores.TryGetValue(primitive.GameId, out var currentScore);
            scores[primitive.GameId] = currentScore + weightedValue;
        }
    }

    private static IgdbPopularityTypeModel? FindPopularityType(
        IEnumerable<IgdbPopularityTypeModel> popularityTypes,
        string expectedName)
    {
        var normalizedExpectedName = NormalizeName(expectedName);

        return popularityTypes
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Name))
            .OrderBy(x => NormalizeName(x.Name) == normalizedExpectedName ? 0 : 1)
            .FirstOrDefault(x =>
                NormalizeName(x.Name).Contains(
                    normalizedExpectedName,
                    StringComparison.Ordinal));
    }

    private static string NormalizeName(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static DateOnly GetAzerbaijanToday()
    {
        return DateOnly.FromDateTime(
            DateTime.UtcNow.AddHours(AzerbaijanUtcOffsetHours));
    }

    private static bool IsDisplayableGame(GameDetailDTO game)
    {
        return game.Id > 0 &&
               !string.IsNullOrWhiteSpace(game.Title) &&
               !string.IsNullOrWhiteSpace(game.CoverImageUrl) &&
               MainCatalogGameTypes.Contains(game.GameType);
    }

    private static bool IsDisplayableGame(IgdbGameModel game)
    {
        return game.Id > 0 &&
               !string.IsNullOrWhiteSpace(game.Name) &&
               !string.IsNullOrWhiteSpace(game.CoverImageUrl) &&
               MainCatalogGameTypes.Contains(game.GameType);
    }

    private static GameListItemDTO MapGame(GameDetailDTO game)
    {
        return new GameListItemDTO
        {
            Source = game.Source,
            Id = game.Id,
            Title = game.Title,
            CoverImageUrl = game.CoverImageUrl,
            ReleaseDate = game.ReleaseDate,
            AverageRating = game.AverageRating,
            IsOverridden = game.IsOverridden,
            GameType = game.GameType,
            Genres = game.Genres,
            Platforms = game.Platforms
        };
    }

    private static GameListItemDTO MapGame(IgdbGameModel game)
    {
        return new GameListItemDTO
        {
            Source = CatalogSource.Igdb,
            Id = game.Id,
            Title = game.Name,
            CoverImageUrl = game.CoverImageUrl,
            ReleaseDate = game.ReleaseDate,
            AverageRating = game.Rating,
            IsOverridden = false,
            GameType = game.GameType,
            Genres = game.Genres.Select(genre => new LookupDTO
            {
                Source = CatalogSource.Igdb,
                Id = genre.Id,
                Name = genre.Name,
                IsOverridden = false
            }).ToList(),
            Platforms = game.Platforms.Select(platform => new LookupDTO
            {
                Source = CatalogSource.Igdb,
                Id = platform.Id,
                Name = platform.Name,
                IsOverridden = false
            }).ToList()
        };
    }
}
