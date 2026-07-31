using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models.Steam;
using Ludora.Application.Settings;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.Steam;

public sealed class SteamStoreClient : ISteamStoreClient
{
    private const int WishlistCandidateCount = 100;
    private const int AzerbaijanUtcOffsetHours = 4;
    private const string SteamSearchResultsUrl =
        "https://store.steampowered.com/search/results/";

    private static readonly string[] ExactReleaseDateFormats =
    [
        "d MMM, yyyy",
        "dd MMM, yyyy",
        "MMM d, yyyy",
        "MMM dd, yyyy",
        "d MMM yyyy",
        "dd MMM yyyy"
    ];

    private static readonly Regex SearchResultRowRegex = new(
        @"<a\b(?<attributes>[^>]*\bclass\s*=\s*[""'][^""']*\bsearch_result_row\b[^""']*[""'][^>]*)>(?<content>.*?)</a>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex AppIdRegex = new(
        @"\bdata-ds-appid\s*=\s*[""'](?<value>\d+)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TitleRegex = new(
        @"<span\b[^>]*\bclass\s*=\s*[""'][^""']*\btitle\b[^""']*[""'][^>]*>(?<value>.*?)</span>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex ReleaseDateRegex = new(
        @"<div\b[^>]*\bclass\s*=\s*[""'][^""']*\bsearch_released\b[^""']*[""'][^>]*>(?<value>.*?)</div>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex ImageRegex = new(
        @"<img\b[^>]*\bsrc\s*=\s*[""'](?<value>[^""']+)[""'][^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex HtmlTagRegex = new(
        @"<[^>]+>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly SteamSettings _settings;
    private readonly SteamRequestThrottle _requestThrottle;
    private readonly IAppCache _cache;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SteamStoreClient(
        HttpClient httpClient,
        IOptions<SteamSettings> options,
        SteamRequestThrottle requestThrottle,
        IAppCache cache)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _requestThrottle = requestThrottle;
        _cache = cache;
    }

    public async Task<IReadOnlyList<SteamComingSoonGameModel>> GetComingSoonAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(limit));

        var cacheKey =
            $"steam:store:top-wishlists:v3:{_settings.CountryCode}:{_settings.Language}";

        var cached = await _cache.GetAsync<List<SteamComingSoonGameModel>>(
            cacheKey,
            cancellationToken);
        if (cached is null)
        {
            cached = (await LoadTopWishlistedUpcomingAsync(cancellationToken)).ToList();
            await _cache.SetAsync(
                cacheKey,
                cached,
                TimeSpan.FromMinutes(_settings.ComingSoonCacheMinutes),
                cancellationToken);
        }

        return cached.Take(limit).ToList();
    }

    private async Task<IReadOnlyList<SteamComingSoonGameModel>>
        LoadTopWishlistedUpcomingAsync(CancellationToken cancellationToken)
    {
        var url = QueryHelpers.AddQueryString(
            SteamSearchResultsUrl,
            new Dictionary<string, string?>
            {
                ["query"] = string.Empty,
                ["start"] = "0",
                ["count"] = WishlistCandidateCount.ToString(
                    CultureInfo.InvariantCulture),
                ["dynamic_data"] = string.Empty,
                ["sort_by"] = "_ASC",
                ["snr"] = "1_7_7_230_7",
                ["filter"] = "popularwishlist",
                ["category1"] = "998",
                ["infinite"] = "1",
                ["force_infinite"] = "1",
                ["ignore_preferences"] = "1",
                ["cc"] = _settings.CountryCode,
                ["l"] = "english"
            });

        using var response = await SendWithRetryAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new ExternalServiceException(
                "Steam Store sorğu limiti dolub. Bir qədər sonra yenidən yoxlayın.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalServiceException(
                $"Steam Store Top Wishlists sorğusu {((int)response.StatusCode)} statusu qaytardı.");
        }

        try
        {
            var responseText = await response.Content.ReadAsStringAsync(
                cancellationToken);
            var resultsHtml = ReadResultsHtml(responseText);

            if (string.IsNullOrWhiteSpace(resultsHtml))
                return [];

            var today = DateOnly.FromDateTime(
                DateTime.UtcNow.AddHours(AzerbaijanUtcOffsetHours));
            var maximumReleaseDate = today.AddYears(1);
            var selectedGames = new List<SteamComingSoonGameModel>();
            var seenAppIds = new HashSet<int>();

            foreach (Match rowMatch in SearchResultRowRegex.Matches(resultsHtml))
            {
                var attributes = rowMatch.Groups["attributes"].Value;
                var content = rowMatch.Groups["content"].Value;
                var appIdMatch = AppIdRegex.Match(attributes);

                if (!appIdMatch.Success ||
                    !int.TryParse(
                        appIdMatch.Groups["value"].Value,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var appId) ||
                    appId < 1 ||
                    !seenAppIds.Add(appId))
                {
                    continue;
                }

                var title = ReadElementText(content, TitleRegex);
                var releaseDateText = ReadElementText(content, ReleaseDateRegex);
                var releaseDate = ParseExactReleaseDate(releaseDateText);

                if (string.IsNullOrWhiteSpace(title) ||
                    !releaseDate.HasValue ||
                    releaseDate.Value < today ||
                    releaseDate.Value > maximumReleaseDate)
                {
                    continue;
                }

                var searchCapsuleUrl = ReadImageUrl(content);
                var storeUrl = $"https://store.steampowered.com/app/{appId}/";

                selectedGames.Add(new SteamComingSoonGameModel
                {
                    Rank = selectedGames.Count + 1,
                    AppId = appId,
                    Name = title,
                    HeaderImageUrl = searchCapsuleUrl,
                    SmallCapsuleImageUrl = searchCapsuleUrl,
                    PortraitImageUrl = BuildPortraitImageUrl(appId),
                    ReleaseDateText = releaseDateText,
                    ReleaseDate = releaseDate,
                    StoreUrl = storeUrl
                });
            }

            return selectedGames;
        }
        catch (JsonException exception)
        {
            throw new ExternalServiceException(
                "Steam Store Top Wishlists cavabı oxuna bilmədi.",
                exception);
        }
    }

    private string ReadResultsHtml(string responseText)
    {
        var trimmed = responseText.TrimStart();

        if (!trimmed.StartsWith('{'))
            return responseText;

        var payload = JsonSerializer.Deserialize<SearchResultsEnvelope>(
            responseText,
            _jsonOptions);

        return payload?.ResultsHtml ?? string.Empty;
    }

    private static string? ReadElementText(string html, Regex regex)
    {
        var match = regex.Match(html);

        return match.Success
            ? CleanHtmlText(match.Groups["value"].Value)
            : null;
    }

    private static string? ReadImageUrl(string html)
    {
        var match = ImageRegex.Match(html);

        if (!match.Success)
            return null;

        var value = WebUtility.HtmlDecode(match.Groups["value"].Value)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? CleanHtmlText(string value)
    {
        var withoutTags = HtmlTagRegex.Replace(value, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags)
            ?.Replace('\u00A0', ' ');

        if (string.IsNullOrWhiteSpace(decoded))
            return null;

        return WhitespaceRegex.Replace(decoded, " ").Trim();
    }

    private static DateOnly? ParseExactReleaseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateOnly.TryParseExact(
            value,
            ExactReleaseDateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var releaseDate)
            ? releaseDate
            : null;
    }

    private static string BuildPortraitImageUrl(int appId)
    {
        return
            $"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{appId}/library_600x900.jpg";
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        string relativeOrAbsoluteUrl,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt <= _settings.RetryCount; attempt++)
        {
            try
            {
                using var lease = await _requestThrottle.EnterAsync(cancellationToken);
                var response = await _httpClient.GetAsync(
                    relativeOrAbsoluteUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                var shouldRetry = response.StatusCode == HttpStatusCode.TooManyRequests ||
                                  (int)response.StatusCode >= 500;

                if (!shouldRetry || attempt == _settings.RetryCount)
                    return response;

                var retryAfter = response.Headers.RetryAfter?.Delta;
                response.Dispose();
                await Task.Delay(
                    retryAfter ?? TimeSpan.FromMilliseconds(300 * Math.Pow(2, attempt)),
                    cancellationToken);
            }
            catch (Exception exception) when (
                (exception is HttpRequestException or TaskCanceledException) &&
                !cancellationToken.IsCancellationRequested)
            {
                lastException = exception;

                if (attempt == _settings.RetryCount)
                {
                    throw new ExternalServiceException(
                        "Steam Store sorğusu tamamlanmadı.",
                        exception);
                }

                await Task.Delay(
                    TimeSpan.FromMilliseconds(300 * Math.Pow(2, attempt)),
                    cancellationToken);
            }
        }

        throw new ExternalServiceException(
            "Steam Store sorğusu tamamlanmadı.",
            lastException ?? new HttpRequestException("Steam Store sorğusu uğursuz oldu."));
    }

    private sealed class SearchResultsEnvelope
    {
        [JsonPropertyName("results_html")]
        public string? ResultsHtml { get; init; }
    }
}
