using System.Text;

using Ludora.Application.DTOs;
using Ludora.Application.DTOs.GameList;
using Ludora.Application.DTOs.Home;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models;
using Ludora.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Ludora.Infrastructure.IGDB
{
    public sealed class HomeDiscoveryService : IHomeDiscoveryService
    {
        private const string FreshCacheKey = "home-discovery:v7:fresh";
        private const string StaleCacheKey = "home-discovery:v7:stale";
        private const int SectionSize = 18;
        private const int ComingSoonSectionSize = 18;
        private const int PopularityCandidateCount = 80;
        private const int UpcomingCandidateCount = 200;
        private const int UpcomingWindowMonths = 18;
        private const int AzerbaijanUtcOffsetHours = 4;
        private static readonly TimeSpan FreshCacheDuration = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan EmptyCacheDuration = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan StaleCacheDuration = TimeSpan.FromHours(24);

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

                var hasDiscoveryData =
                    result.TrendingGames.Count > 0 ||
                    result.ComingSoonGames.Count > 0;

                await _cache.SetAsync(
                    FreshCacheKey,
                    result,
                    hasDiscoveryData
                        ? FreshCacheDuration
                        : EmptyCacheDuration,
                    cancellationToken);

                // Never replace the long-lived last-known-good cache with a
                // completely empty response caused by a temporary IGDB/SQL issue.
                if (hasDiscoveryData)
                {
                    await _cache.SetAsync(
                        StaleCacheKey,
                        result,
                        StaleCacheDuration,
                        cancellationToken);
                }

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
            IReadOnlyList<GameListItemDTO> trendingGames =
                staleResult?.TrendingGames ?? [];
            IReadOnlyList<HomeComingSoonGameDTO> comingSoonGames =
                staleResult?.ComingSoonGames ?? [];

            if (!_igdbClient.IsEnabled)
            {
                return new HomeDiscoveryDTO
                {
                    TrendingGames = trendingGames,
                    ComingSoonGames = comingSoonGames,
                    GeneratedAtUtc = DateTime.UtcNow
                };
            }

            IReadOnlyList<IgdbPopularityTypeModel> popularityTypes = [];

            try
            {
                popularityTypes = await _igdbClient
                    .GetPopularityTypesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Popularity metadata is useful for ranking, but it must not
                // take both home rails down. Each rail has an IGDB games fallback.
                _logger.LogWarning(
                    exception,
                    "IGDB popularity type məlumatları alınmadı; discovery fallback ilə davam edir.");
            }

            try
            {
                var loadedTrendingGames = await LoadTrendingFromIgdbAsync(
                    popularityTypes,
                    cancellationToken);

                if (loadedTrendingGames.Count > 0)
                    trendingGames = loadedTrendingGames;
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
                    "Trend oyunları alına bilmədi; son uğurlu keş istifadə olunur.");
            }

            try
            {
                var loadedComingSoonGames =
                    await LoadMostAnticipatedUpcomingAsync(
                        popularityTypes,
                        cancellationToken);

                if (loadedComingSoonGames.Count > 0)
                    comingSoonGames = loadedComingSoonGames;
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
                    "Tezliklə çıxacaq oyunlar alına bilmədi; son uğurlu keş istifadə olunur.");
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

            var gamesById = await ResolveIgdbGameCardsAsync(
                trendingIds,
                cancellationToken);

            var trendingGames = trendingIds
                .Select(id => gamesById.GetValueOrDefault(id))
                .Where(x => x is not null)
                .Select(x => x!)
                .Where(x =>
                    x.ReleaseDate.HasValue &&
                    x.ReleaseDate.Value <= today)
                .Take(SectionSize)
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
            var wishlistedTask = LoadPopularityMetricSafeAsync(
                popularityTypes,
                "Most Wishlisted Upcoming",
                UpcomingCandidateCount,
                cancellationToken);
            var wantToPlayTask = LoadPopularityMetricSafeAsync(
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
                return await LoadUpcomingFallbackAsync(
                    today,
                    maximumReleaseDate,
                    cancellationToken);

            var gamesById = await ResolveIgdbGameCardsAsync(
                candidateIds,
                cancellationToken);

            var releaseWindowDays = Math.Max(
                1,
                maximumReleaseDate.DayNumber - today.DayNumber);

            var rankedGames = candidateIds
                .Select(id => gamesById.GetValueOrDefault(id))
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

        private async Task<IReadOnlyList<HomeComingSoonGameDTO>>
            LoadUpcomingFallbackAsync(
                DateOnly today,
                DateOnly maximumReleaseDate,
                CancellationToken cancellationToken)
        {
            var games = await _igdbClient.GetUpcomingGamesAsync(
                today,
                maximumReleaseDate,
                Math.Max(ComingSoonSectionSize * 3, 36),
                cancellationToken);

            return games
                .Where(IsDisplayableGame)
                .Where(x =>
                    x.ReleaseDate.HasValue &&
                    x.ReleaseDate.Value >= today &&
                    x.ReleaseDate.Value <= maximumReleaseDate)
                .Take(ComingSoonSectionSize)
                .Select((game, index) => new HomeComingSoonGameDTO
                {
                    AnticipationRank = index + 1,
                    Source = CatalogSource.Igdb,
                    Id = game.Id,
                    Title = game.Name,
                    CoverImageUrl = game.CoverImageUrl,
                    ReleaseDate = game.ReleaseDate
                })
                .ToList();
        }

        private async Task<IReadOnlyDictionary<long, GameListItemDTO>>
            ResolveIgdbGameCardsAsync(
                IReadOnlyCollection<long> gameIds,
                CancellationToken cancellationToken)
        {
            var normalizedIds = gameIds
                .Where(x => x > 0)
                .Distinct()
                .Take(500)
                .ToArray();

            if (normalizedIds.Length == 0)
                return new Dictionary<long, GameListItemDTO>();

            var result = new Dictionary<long, GameListItemDTO>();

            try
            {
                var references = normalizedIds
                    .Select(id => new CatalogReferenceDTO
                    {
                        Source = CatalogSource.Igdb,
                        Id = id
                    })
                    .ToArray();

                var gamesByReference =
                    await _gameService.GetByReferencesAsync(
                        references,
                        cancellationToken);

                foreach (var id in normalizedIds)
                {
                    var game = gamesByReference.GetValueOrDefault(
                        (CatalogSource.Igdb, id));

                    if (game is null || !IsDisplayableGame(game))
                        continue;

                    result[id] = MapGame(game);
                }

                // A successful GameService result has already applied local
                // overrides/hidden-game rules. Do not re-add omitted games.
                return result;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Discovery should still work when SQL/override enrichment has a
                // transient failure. The card endpoint below is IGDB-only.
                _logger.LogWarning(
                    exception,
                    "Discovery oyunları DB zənginləşdirməsindən alına bilmədi; birbaşa IGDB fallback istifadə olunur.");
            }

            var igdbGames = await _igdbClient.GetGamesByIdsForCardsAsync(
                normalizedIds,
                cancellationToken);

            foreach (var game in igdbGames.Where(IsDisplayableGame))
                result[game.Id] = MapGame(game);

            return result;
        }

        private async Task<IReadOnlyList<IgdbPopularityPrimitiveModel>>
            LoadPopularityMetricSafeAsync(
                IReadOnlyList<IgdbPopularityTypeModel> popularityTypes,
                string metricName,
                int limit,
                CancellationToken cancellationToken)
        {
            try
            {
                return await LoadPopularityMetricAsync(
                    popularityTypes,
                    metricName,
                    limit,
                    cancellationToken);
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
                    "IGDB popularity metric alınmadı: {MetricName}",
                    metricName);
                return [];
            }
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
                    Primitives = await LoadPopularityMetricSafeAsync(
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

        private static bool IsDisplayableGame(GameListItemDTO game)
        {
            return game.Id > 0 &&
                   !string.IsNullOrWhiteSpace(game.Title) &&
                   !string.IsNullOrWhiteSpace(game.CoverImageUrl) &&
                   MainCatalogGameTypes.Contains(game.GameType);
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
}
