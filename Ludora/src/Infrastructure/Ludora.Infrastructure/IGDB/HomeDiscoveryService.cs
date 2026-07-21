using System.Text;
using Ludora.Application.DTOs;
using Ludora.Application.DTOs.GameList;
using Ludora.Application.DTOs.Home;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models;
using Ludora.Domain.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Ludora.Infrastructure.IGDB;

public sealed class HomeDiscoveryService : IHomeDiscoveryService
{
    private const string FreshCacheKey = "home-discovery:igdb:v2:fresh";
    private const string StaleCacheKey = "home-discovery:igdb:v2:stale";
    private const int SectionSize = 6;
    private const int PopularityCandidateCount = 80;

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
    private readonly IMemoryCache _cache;
    private readonly ILogger<HomeDiscoveryService> _logger;

    public HomeDiscoveryService(
        IIgdbClient igdbClient,
        IGameService gameService,
        IMemoryCache cache,
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
        if (!_igdbClient.IsEnabled)
            return CreateEmptyResult();

        if (_cache.TryGetValue(
                FreshCacheKey,
                out HomeDiscoveryDTO? cachedResult) &&
            cachedResult is not null)
        {
            return cachedResult;
        }

        await CacheLock.WaitAsync(cancellationToken);

        try
        {
            if (_cache.TryGetValue(
                    FreshCacheKey,
                    out cachedResult) &&
                cachedResult is not null)
            {
                return cachedResult;
            }

            try
            {
                var result = await LoadFromIgdbAsync(cancellationToken);

                _cache.Set(
                    FreshCacheKey,
                    result,
                    TimeSpan.FromMinutes(30));

                _cache.Set(
                    StaleCacheKey,
                    result,
                    TimeSpan.FromHours(24));

                return result;
            }
            catch (ExternalServiceException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Ana səhifə üçün IGDB trend məlumatları alına bilmədi.");

                if (_cache.TryGetValue(
                        StaleCacheKey,
                        out HomeDiscoveryDTO? staleResult) &&
                    staleResult is not null)
                {
                    return staleResult;
                }

                return CreateEmptyResult();
            }
        }
        finally
        {
            CacheLock.Release();
        }
    }

    private async Task<HomeDiscoveryDTO> LoadFromIgdbAsync(
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var popularityTypes = await _igdbClient.GetPopularityTypesAsync(
            cancellationToken);

        var trendingScores = await BuildTrendingScoresAsync(
            popularityTypes,
            cancellationToken);

        var comingSoonScores = await BuildComingSoonScoresAsync(
            popularityTypes,
            cancellationToken);

        var fallbackUpcomingGames = await _igdbClient.GetUpcomingGamesAsync(
            today.AddDays(1),
            today.AddYears(1),
            40,
            cancellationToken);

        var trendingIds = trendingScores
            .OrderByDescending(x => x.Value)
            .Select(x => x.Key)
            .ToArray();

        var comingSoonIds = comingSoonScores
            .OrderByDescending(x => x.Value)
            .Select(x => x.Key)
            .Concat(fallbackUpcomingGames.Select(x => x.Id))
            .Where(x => x > 0)
            .Distinct()
            .Take(150)
            .ToArray();

        var references = trendingIds
            .Concat(comingSoonIds)
            .Where(x => x > 0)
            .Distinct()
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

        var comingSoonGames = comingSoonIds
            .Select(id => gamesByReference.GetValueOrDefault(
                (CatalogSource.Igdb, id)))
            .Where(x => x is not null)
            .Select(x => x!)
            .Where(IsDisplayableGame)
            .Where(x => IsUpcomingWithinOneYear(x, today))
            .Take(SectionSize)
            .Select(MapGame)
            .ToList();

        return new HomeDiscoveryDTO
        {
            TrendingGames = trendingGames,
            ComingSoonGames = comingSoonGames,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private async Task<Dictionary<long, double>> BuildTrendingScoresAsync(
        IReadOnlyList<IgdbPopularityTypeModel> popularityTypes,
        CancellationToken cancellationToken)
    {
        var scores = new Dictionary<long, double>();

        foreach (var metric in TrendingMetrics)
        {
            var popularityType = FindPopularityType(
                popularityTypes,
                metric.Name);

            if (popularityType is null)
                continue;

            var primitives = await _igdbClient.GetPopularityPrimitivesAsync(
                popularityType.Id,
                PopularityCandidateCount,
                cancellationToken);

            AddNormalizedScores(scores, primitives, metric.Weight);
        }

        return scores
            .OrderByDescending(x => x.Value)
            .Take(PopularityCandidateCount)
            .ToDictionary(x => x.Key, x => x.Value);
    }

    private async Task<Dictionary<long, double>> BuildComingSoonScoresAsync(
        IReadOnlyList<IgdbPopularityTypeModel> popularityTypes,
        CancellationToken cancellationToken)
    {
        var popularityType = FindPopularityType(
            popularityTypes,
            "Most Wishlisted Upcoming");

        if (popularityType is null)
            return [];

        var primitives = await _igdbClient.GetPopularityPrimitivesAsync(
            popularityType.Id,
            PopularityCandidateCount,
            cancellationToken);

        return primitives
            .Where(x => x.GameId > 0 && x.Value > 0)
            .GroupBy(x => x.GameId)
            .ToDictionary(
                x => x.Key,
                x => (double)x.Max(item => item.Value));
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

    private static bool IsDisplayableGame(GameDetailDTO game)
    {
        return game.Id > 0 &&
               !string.IsNullOrWhiteSpace(game.Title) &&
               !string.IsNullOrWhiteSpace(game.CoverImageUrl) &&
               MainCatalogGameTypes.Contains(game.GameType);
    }

    private static bool IsUpcomingWithinOneYear(
        GameDetailDTO game,
        DateOnly today)
    {
        return game.ReleaseDate.HasValue &&
               game.ReleaseDate.Value > today &&
               game.ReleaseDate.Value <= today.AddYears(1);
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

    private static HomeDiscoveryDTO CreateEmptyResult()
    {
        return new HomeDiscoveryDTO
        {
            GeneratedAtUtc = DateTime.UtcNow
        };
    }
}
