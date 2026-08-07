using System.Text;
using System.Text.Json;

using Ludora.Application.DTOs;
using Ludora.Application.DTOs.GameList;
using Ludora.Application.DTOs.Home;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models;
using Ludora.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Ludora.Infrastructure.FpsResearch
{
    public sealed class HomeInterestRecommendationService
        : IHomeInterestRecommendationService
    {
        private const int ResearchResultLimit = 8;
        private const int RecommendationLimit = 8;

        private static readonly HashSet<GameType> DisplayableGameTypes =
        [
            GameType.MainGame,
            GameType.StandaloneExpansion,
            GameType.Remake,
            GameType.Remaster,
            GameType.ExpandedGame,
            GameType.Port
        ];

        private readonly IUserService _userService;
        private readonly IFpsResearchClient _researchClient;
        private readonly IIgdbClient _igdbClient;
        private readonly ILogger<HomeInterestRecommendationService> _logger;

        public HomeInterestRecommendationService(
            IUserService userService,
            IFpsResearchClient researchClient,
            IIgdbClient igdbClient,
            ILogger<HomeInterestRecommendationService> logger)
        {
            _userService = userService;
            _researchClient = researchClient;
            _igdbClient = igdbClient;
            _logger = logger;
        }

        public async Task<HomeInterestRecommendationsDTO> GetAsync(
            Guid userId,
            string language,
            CancellationToken cancellationToken = default)
        {
            var profile = await _userService.GetForumInterestsAsync(
                userId,
                cancellationToken);

            if (profile is null)
                return Empty(language);

            var normalizedLanguage = NormalizeLanguage(language);
            var saved = await _userService
                .GetSavedHomeInterestRecommendationsAsync(
                    userId,
                    cancellationToken);

            if (saved is { Items.Count: > 0 } &&
                string.Equals(
                    saved.Language,
                    normalizedLanguage,
                    StringComparison.OrdinalIgnoreCase))
            {
                return saved;
            }

            if (!_igdbClient.IsEnabled)
            {
                throw new ExternalServiceException(
                    "Oyun kataloqu xidməti hazırda aktiv deyil.");
            }

            if (!_researchClient.IsEnabled)
            {
                return await BuildIgdbFallbackAsync(
                    profile,
                    normalizedLanguage,
                    cancellationToken);
            }

            HomeInterestRecommendationsDTO result;

            try
            {
                result = await ResearchWithGroqAsync(
                    profile,
                    normalizedLanguage,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
                when (exception is ExternalServiceException or JsonException)
            {
                _logger.LogWarning(
                    exception,
                    "Groq interest recommendation uğursuz oldu; IGDB popularity fallback istifadə edilir.");

                return await BuildIgdbFallbackAsync(
                    profile,
                    normalizedLanguage,
                    cancellationToken);
            }

            if (result.Items.Count == 0)
            {
                _logger.LogWarning(
                    "Groq interest recommendation cavabı uyğun kataloq oyunu vermədi; IGDB popularity fallback istifadə edilir.");

                return await BuildIgdbFallbackAsync(
                    profile,
                    normalizedLanguage,
                    cancellationToken);
            }

            return await _userService.SaveHomeInterestRecommendationsOnceAsync(
                userId,
                result,
                cancellationToken);
        }

        private async Task<HomeInterestRecommendationsDTO> ResearchWithGroqAsync(
            ForumInterestProfileDTO profile,
            string language,
            CancellationToken cancellationToken)
        {
            var completion = await _researchClient.ResearchAsync(
                BuildSystemPrompt(language),
                BuildResearchPrompt(profile),
                cancellationToken,
                "groq/compound-mini",
                "latest");
            var ranking = ReadRanking(completion.Content);
            var selectedGenres = profile.Genres
                .Select(Normalize)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var selectedTitles = profile.Games
                .Select(game => NormalizeTitle(game.Title))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var requestedItems = (ranking.Recommendations ?? [])
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.Title) &&
                    HasConcretePopularityEvidence(item.PopularitySignal) &&
                    !selectedTitles.Contains(NormalizeTitle(item.Title)))
                .Take(ResearchResultLimit)
                .ToArray();
            var resolvedItems = await Task.WhenAll(
                requestedItems.Select(item => ResolveSafelyAsync(
                    item,
                    cancellationToken)));
            var recommendations = new List<HomeInterestRecommendationDTO>();

            foreach (var resolved in resolvedItems)
            {
                if (resolved.Game is null)
                    continue;

                var game = resolved.Game;
                var matchesGenre = selectedGenres.Count == 0 ||
                    game.Genres.Any(genre =>
                        selectedGenres.Contains(Normalize(genre.Name)));
                var matchesPlatform = string.IsNullOrWhiteSpace(profile.Platform) ||
                    game.Platforms.Any(platform =>
                        PlatformMatches(profile.Platform, platform.Name));

                if (!matchesGenre ||
                    !matchesPlatform ||
                    selectedTitles.Contains(NormalizeTitle(game.Title)) ||
                    recommendations.Any(existing =>
                        existing.Game.Source == game.Source &&
                        existing.Game.Id == game.Id))
                {
                    continue;
                }

                recommendations.Add(new HomeInterestRecommendationDTO
                {
                    Game = game,
                    Reason = BuildResearchReason(resolved.Item, language),
                    MatchScore = Math.Clamp(resolved.Item.MatchScore, 1, 100)
                });

                if (recommendations.Count == RecommendationLimit)
                    break;
            }

            return new HomeInterestRecommendationsDTO
            {
                Language = language,
                Items = recommendations,
                Summary = string.IsNullOrWhiteSpace(ranking.Summary)
                    ? language == "en"
                        ? "Researched from public player activity, sales and long-term popularity signals on the web."
                        : "İnternetdəki oyunçu aktivliyi, satış və uzunmüddətli populyarlıq göstəricilərinə əsasən araşdırıldı."
                    : LimitText(ranking.Summary, 260),
                IsAiGenerated = true,
                GeneratedAtUtc = DateTime.UtcNow
            };
        }

        private async Task<ResolvedRankingItem> ResolveSafelyAsync(
            GroqRankingItem item,
            CancellationToken cancellationToken)
        {
            try
            {
                return new ResolvedRankingItem(
                    item,
                    await ResolveResearchedGameAsync(
                        item.Title,
                        cancellationToken));
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
                    "AI recommendation title could not be verified in IGDB. Title {Title}.",
                    item.Title);
                return new ResolvedRankingItem(item, null);
            }
        }

        private async Task<GameListItemDTO?> ResolveResearchedGameAsync(
            string requestedTitle,
            CancellationToken cancellationToken)
        {
            var normalizedRequestedTitle = NormalizeTitle(requestedTitle);
            var searchResults = await _igdbClient.SearchGamesIncludingVersionsAsync(
                requestedTitle.Trim(),
                10,
                cancellationToken);
            var matchedGame = searchResults
                .Where(game =>
                    game.Id > 0 &&
                    !string.IsNullOrWhiteSpace(game.Name) &&
                    !string.IsNullOrWhiteSpace(game.CoverImageUrl) &&
                    DisplayableGameTypes.Contains(game.GameType))
                .OrderByDescending(game =>
                    NormalizeTitle(game.Name) == normalizedRequestedTitle)
                .ThenByDescending(game => game.Rating ?? 0)
                .FirstOrDefault(game =>
                {
                    var normalizedGameTitle = NormalizeTitle(game.Name);

                    return normalizedGameTitle == normalizedRequestedTitle ||
                           normalizedGameTitle.Contains(
                               normalizedRequestedTitle,
                               StringComparison.Ordinal) ||
                           normalizedRequestedTitle.Contains(
                               normalizedGameTitle,
                               StringComparison.Ordinal);
                });

            return matchedGame is null ? null : MapGame(matchedGame);
        }

        private async Task<HomeInterestRecommendationsDTO> BuildIgdbFallbackAsync(
            ForumInterestProfileDTO profile,
            string language,
            CancellationToken cancellationToken)
        {
            try
            {
                var selectedGenres = profile.Genres
                    .Select(Normalize)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var selectedTitles = profile.Games
                    .Select(game => NormalizeTitle(game.Title))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var popularityTypes = await _igdbClient
                    .GetPopularityTypesAsync(cancellationToken);
                var popularityType = FindFallbackPopularityType(popularityTypes);
                IReadOnlyList<IgdbPopularityPrimitiveModel> primitives = [];

                if (popularityType is not null)
                {
                    primitives = await _igdbClient.GetPopularityPrimitivesAsync(
                        popularityType.Id,
                        120,
                        cancellationToken);
                }

                var popularityScore = primitives
                    .GroupBy(item => item.GameId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Max(item => item.Value));
                var gameIds = primitives
                    .OrderByDescending(item => item.Value)
                    .Select(item => item.GameId)
                    .Distinct()
                    .Take(120)
                    .ToArray();

                IReadOnlyList<IgdbGameModel> games = gameIds.Length > 0
                    ? await _igdbClient.GetGamesByIdsForCardsAsync(
                        gameIds,
                        cancellationToken)
                    : [];

                var candidates = FilterFallbackGames(
                        games,
                        profile,
                        selectedGenres,
                        selectedTitles)
                    .OrderByDescending(game =>
                        popularityScore.GetValueOrDefault(game.Id))
                    .ThenByDescending(game => game.Rating ?? 0)
                    .ToList();

                if (candidates.Count < RecommendationLimit)
                {
                    var genreFilters = await ResolveGenreFiltersAsync(
                        profile.Genres,
                        cancellationToken);
                    var catalogGames = await _igdbClient.GetGamesAsync(
                        new GameFilterDTO
                        {
                            GenreFilters = genreFilters,
                            ReleasedOnly = true,
                            SortBy = "rating_desc",
                            PageNumber = 1,
                            PageSize = 80,
                            IncludeRelatedContent = false
                        },
                        cancellationToken);

                    foreach (var game in FilterFallbackGames(
                                 catalogGames,
                                 profile,
                                 selectedGenres,
                                 selectedTitles))
                    {
                        if (candidates.Any(existing => existing.Id == game.Id))
                            continue;

                        candidates.Add(game);
                        if (candidates.Count >= RecommendationLimit)
                            break;
                    }
                }

                var items = candidates
                    .Take(RecommendationLimit)
                    .Select((game, index) => new HomeInterestRecommendationDTO
                    {
                        Game = MapGame(game),
                        Reason = BuildFallbackReason(game, profile, language),
                        MatchScore = Math.Max(70, 92 - index * 3)
                    })
                    .ToList();

                if (items.Count == 0)
                {
                    throw new ExternalServiceException(
                        "Seçimlərinizə uyğun oyun tövsiyəsi hazırda tapılmadı.");
                }

                return new HomeInterestRecommendationsDTO
                {
                    Language = language,
                    Items = items,
                    Summary = language == "en"
                        ? "Groq is temporarily unavailable, so these picks use IGDB popularity and your saved interests as a fallback."
                        : "Groq müvəqqəti əlçatan olmadığı üçün bu seçimlər IGDB populyarlığı və yadda saxladığın maraqlara əsaslanan fallback ilə hazırlanıb.",
                    IsAiGenerated = false,
                    GeneratedAtUtc = DateTime.UtcNow
                };
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (ExternalServiceException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "IGDB interest recommendation fallback uğursuz oldu.");

                throw new ExternalServiceException(
                    "Oyun tövsiyə xidməti hazırda əlçatan deyil. Bir qədər sonra yenidən yoxlayın.",
                    exception);
            }
        }

        private static IEnumerable<IgdbGameModel> FilterFallbackGames(
            IEnumerable<IgdbGameModel> games,
            ForumInterestProfileDTO profile,
            IReadOnlySet<string> selectedGenres,
            IReadOnlySet<string> selectedTitles)
        {
            return games.Where(game =>
                game.Id > 0 &&
                !string.IsNullOrWhiteSpace(game.Name) &&
                !string.IsNullOrWhiteSpace(game.CoverImageUrl) &&
                DisplayableGameTypes.Contains(game.GameType) &&
                !selectedTitles.Contains(NormalizeTitle(game.Name)) &&
                (selectedGenres.Count == 0 ||
                 game.Genres.Any(genre =>
                     selectedGenres.Contains(Normalize(genre.Name)))) &&
                (string.IsNullOrWhiteSpace(profile.Platform) ||
                 game.Platforms.Any(platform =>
                     PlatformMatches(profile.Platform, platform.Name))));
        }

        private async Task<string?> ResolveGenreFiltersAsync(
            IEnumerable<string> genreNames,
            CancellationToken cancellationToken)
        {
            var ids = new HashSet<long>();

            foreach (var genreName in genreNames
                         .Where(name => !string.IsNullOrWhiteSpace(name))
                         .Take(8))
            {
                var normalizedName = Normalize(genreName);
                var matches = await _igdbClient.GetGenresAsync(
                    genreName,
                    cancellationToken);
                var match = matches.FirstOrDefault(genre =>
                                Normalize(genre.Name) == normalizedName)
                            ?? matches.FirstOrDefault();

                if (match is not null && match.Id > 0)
                    ids.Add(match.Id);
            }

            return ids.Count == 0
                ? null
                : string.Join(',', ids.Select(id => $"Igdb:{id}"));
        }

        private static IgdbPopularityTypeModel? FindFallbackPopularityType(
            IEnumerable<IgdbPopularityTypeModel> popularityTypes)
        {
            var types = popularityTypes.ToArray();
            var preferredNames = new[]
            {
                "Playing",
                "Visits",
                "Want to Play"
            };

            foreach (var preferredName in preferredNames)
            {
                var match = types.FirstOrDefault(type =>
                    string.Equals(
                        type.Name,
                        preferredName,
                        StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                    return match;
            }

            return types.FirstOrDefault();
        }

        private static string BuildFallbackReason(
            IgdbGameModel game,
            ForumInterestProfileDTO profile,
            string language)
        {
            var matchingGenres = game.Genres
                .Select(genre => genre.Name)
                .Where(name => profile.Genres.Any(selected =>
                    Normalize(selected) == Normalize(name)))
                .Take(2)
                .ToArray();
            var genreText = matchingGenres.Length > 0
                ? string.Join(", ", matchingGenres)
                : game.Genres.FirstOrDefault()?.Name;

            return language == "en"
                ? LimitText(
                    string.IsNullOrWhiteSpace(genreText)
                        ? "A popular catalog pick that matches your saved platform and game interests."
                        : $"A popular {genreText} pick that matches your saved platform and game interests.",
                    270)
                : LimitText(
                    string.IsNullOrWhiteSpace(genreText)
                        ? "Yadda saxladığın platforma və oyun maraqlarına uyğun populyar kataloq seçimidir."
                        : $"Yadda saxladığın platforma və oyun maraqlarına uyğun populyar {genreText} seçimidir.",
                    270);
        }

        private static string BuildSystemPrompt(string language)
        {
            var outputLanguage = language == "en" ? "English" : "Azerbaijani";

            return $$"""
            You are WellPlayd's game popularity researcher. You MUST use web search before answering.
            Run exactly one focused search and prefer official milestones, major platform charts and established trackers.
            Rank established games by all-time mainstream player reach, never by review score, novelty or recent release.
            A candidate is eligible only when public evidence shows at least one of these thresholds: 5 million copies
            sold, 10 million registered/active players, 100,000 peak concurrent players, or sustained placement on a
            major platform's most-played chart. Exclude obscure and niche games without that scale even if highly rated.
            Every result must genuinely match at least one selected genre; play styles are important ranking preferences;
            the selected main platform is mandatory. Exclude every supplied favorite game. Use exact official titles for
            IGDB verification. Never invent a game or a metric. popularitySignal must contain the concrete numeric figure
            or chart evidence plus its source/timeframe. Return exactly 8 eligible candidates when possible, as JSON only:
            {"summary":"short research summary","recommendations":[{"title":"Exact official title","reason":"why it fits the genres and play styles","popularitySignal":"credible popularity evidence","matchScore":90}]}
            Write summary, reason and popularitySignal in {{outputLanguage}}.
            """;
        }

        private static string BuildResearchPrompt(ForumInterestProfileDTO profile) =>
            JsonSerializer.Serialize(new
            {
                task = "Find the most-played and most-popular games matching these exact interests.",
                selectedGenres = profile.Genres,
                selectedPlayStyles = profile.Styles,
                mainPlatform = profile.Platform,
                excludeFavoriteGames = profile.Games.Select(game => game.Title),
                resultCount = ResearchResultLimit
            });

        private static GroqRankingEnvelope ReadRanking(string content)
        {
            var normalized = content.Trim();
            var firstBrace = normalized.IndexOf('{');
            var lastBrace = normalized.LastIndexOf('}');

            if (firstBrace < 0 || lastBrace <= firstBrace)
                throw new JsonException("Groq response did not contain JSON.");

            return JsonSerializer.Deserialize<GroqRankingEnvelope>(
                       normalized[firstBrace..(lastBrace + 1)],
                       new JsonSerializerOptions
                       {
                           PropertyNameCaseInsensitive = true
                       }) ?? throw new JsonException("Groq ranking was empty.");
        }

        private static bool HasConcretePopularityEvidence(string? signal)
        {
            if (string.IsNullOrWhiteSpace(signal) ||
                !signal.Any(char.IsDigit))
            {
                return false;
            }

            var normalized = Normalize(signal);

            return normalized.Contains("million", StringComparison.Ordinal) ||
                   normalized.Contains("milyon", StringComparison.Ordinal) ||
                   normalized.Contains("player", StringComparison.Ordinal) ||
                   normalized.Contains("oyuncu", StringComparison.Ordinal) ||
                   normalized.Contains("copies", StringComparison.Ordinal) ||
                   normalized.Contains("copy", StringComparison.Ordinal) ||
                   normalized.Contains("sat", StringComparison.Ordinal) ||
                   normalized.Contains("concurrent", StringComparison.Ordinal) ||
                   normalized.Contains("peak", StringComparison.Ordinal) ||
                   normalized.Contains("chart", StringComparison.Ordinal);
        }

        private static string BuildResearchReason(
            GroqRankingItem item,
            string language)
        {
            var reason = LimitText(item.Reason, 170);
            var popularitySignal = LimitText(item.PopularitySignal, 110);

            if (string.IsNullOrWhiteSpace(popularitySignal))
                return reason;
            if (string.IsNullOrWhiteSpace(reason))
                return popularitySignal;

            return LimitText(
                language == "en"
                    ? $"{reason} Popularity: {popularitySignal}"
                    : $"{reason} Populyarlıq: {popularitySignal}",
                270);
        }

        private static bool PlatformMatches(string selected, string candidate)
        {
            var normalizedCandidate = candidate.Trim().ToLowerInvariant();

            return selected.Trim().ToLowerInvariant() switch
            {
                "pc" => normalizedCandidate.Contains("pc") ||
                        normalizedCandidate.Contains("windows"),
                "playstation" => normalizedCandidate.Contains("playstation"),
                "xbox" => normalizedCandidate.Contains("xbox"),
                "nintendo" => normalizedCandidate.Contains("nintendo switch"),
                "mobile" => normalizedCandidate is "android" or "ios",
                _ => normalizedCandidate.Contains(
                    selected.Trim().ToLowerInvariant(),
                    StringComparison.Ordinal)
            };
        }

        private static GameListItemDTO MapGame(IgdbGameModel game) => new()
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

        private static string Normalize(string value) =>
            string.Join(' ', value.Trim().ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

        private static string NormalizeTitle(string value) =>
            new(value
                .Normalize(NormalizationForm.FormKC)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());

        private static string LimitText(string? value, int maxLength)
        {
            var normalized = value?.Trim() ?? string.Empty;

            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength].TrimEnd() + "…";
        }

        private static string NormalizeLanguage(string? language) =>
            string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
                ? "en"
                : "az";

        private static HomeInterestRecommendationsDTO Empty(string language)
        {
            var normalizedLanguage = NormalizeLanguage(language);

            return new HomeInterestRecommendationsDTO
            {
                Language = normalizedLanguage,
                Summary = normalizedLanguage == "en"
                ? "Choose your interests to receive personalized game recommendations."
                    : "Fərdi oyun tövsiyələri almaq üçün maraqlarını seç.",
                GeneratedAtUtc = DateTime.UtcNow
            };
        }

        private sealed record ResolvedRankingItem(
            GroqRankingItem Item,
            GameListItemDTO? Game);

        private sealed record GroqRankingEnvelope
        {
            public string Summary { get; init; } = string.Empty;
            public IReadOnlyList<GroqRankingItem>? Recommendations { get; init; }
        }

        private sealed record GroqRankingItem
        {
            public string Title { get; init; } = string.Empty;
            public string Reason { get; init; } = string.Empty;
            public string PopularitySignal { get; init; } = string.Empty;
            public int MatchScore { get; init; }
        }
    }
}
