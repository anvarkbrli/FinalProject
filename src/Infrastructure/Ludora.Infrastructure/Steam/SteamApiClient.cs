using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models.Steam;
using Ludora.Application.Settings;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.Steam;

public sealed class SteamApiClient : ISteamApiClient
{
    private const int MaxSteamIdsPerRequest = 100;
    private readonly HttpClient _httpClient;
    private readonly SteamSettings _settings;
    private readonly SteamRequestThrottle _requestThrottle;
    private readonly IAppCache _cache;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SteamApiClient(
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

    public bool IsEnabled => _settings.Enabled;

    public async Task<SteamPlayerProfileModel?> GetPlayerSummaryAsync(
        string steamId,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ValidateSteamId(steamId);

        var cacheKey = $"steam:profile:{steamId}";

        if (!bypassCache)
        {
            var cached = await _cache.GetAsync<SteamPlayerProfileModel>(
                cacheKey,
                cancellationToken);
            if (cached is not null)
                return cached;
        }

        var profiles = await GetPlayerSummariesCoreAsync([steamId], cancellationToken);
        var profile = profiles.FirstOrDefault();

        if (profile is not null)
        {
            await _cache.SetAsync(
                cacheKey,
                profile,
                TimeSpan.FromSeconds(_settings.ProfileCacheSeconds),
                cancellationToken);
        }

        return profile;
    }

    public async Task<IReadOnlyList<SteamPlayerProfileModel>> GetPlayerSummariesAsync(
        IReadOnlyCollection<string> steamIds,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ArgumentNullException.ThrowIfNull(steamIds);

        var validIds = steamIds
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var steamId in validIds)
            ValidateSteamId(steamId);

        var result = new List<SteamPlayerProfileModel>(validIds.Length);

        foreach (var batch in validIds.Chunk(MaxSteamIdsPerRequest))
        {
            result.AddRange(await GetPlayerSummariesCoreAsync(batch, cancellationToken));
        }

        return result;
    }

    public async Task<SteamOwnedGamesResultModel> GetOwnedGamesAsync(
        string steamId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ValidateSteamId(steamId);

        var url = BuildUrl(
            "IPlayerService/GetOwnedGames/v0001/",
            new Dictionary<string, string?>
            {
                ["key"] = _settings.ApiKey,
                ["steamid"] = steamId,
                ["include_appinfo"] = "true",
                ["include_played_free_games"] = "true",
                ["format"] = "json"
            });

        using var response = await SendWithRetryAsync(url, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new SteamOwnedGamesResultModel { IsVisible = false };

        await EnsureSuccessAsync(response, "Steam oyun kitabxanası", cancellationToken);
        var payload = await DeserializeAsync<OwnedGamesEnvelope>(response, cancellationToken);
        var data = payload?.Response;
        var isVisible = data?.GameCount.HasValue == true || data?.Games is not null;

        return new SteamOwnedGamesResultModel
        {
            IsVisible = isVisible,
            GameCount = data?.GameCount ?? data?.Games?.Count ?? 0,
            Games = data?.Games?
                .Where(x => x.AppId > 0)
                .Select(MapOwnedGame)
                .ToList() ?? []
        };
    }

    public async Task<SteamFriendsResultModel> GetFriendsAsync(
        string steamId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ValidateSteamId(steamId);

        var url = BuildUrl(
            "ISteamUser/GetFriendList/v0001/",
            new Dictionary<string, string?>
            {
                ["key"] = _settings.ApiKey,
                ["steamid"] = steamId,
                ["relationship"] = "friend",
                ["format"] = "json"
            });

        using var response = await SendWithRetryAsync(url, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new SteamFriendsResultModel { IsVisible = false };

        await EnsureSuccessAsync(response, "Steam dost siyahısı", cancellationToken);
        var payload = await DeserializeAsync<FriendsEnvelope>(response, cancellationToken);

        if (payload?.FriendsList?.Friends is null)
            return new SteamFriendsResultModel { IsVisible = false };

        return new SteamFriendsResultModel
        {
            IsVisible = true,
            Friends = payload.FriendsList.Friends
                .Where(x => !string.IsNullOrWhiteSpace(x.SteamId))
                .Select(x => new SteamFriendModel
                {
                    SteamId = x.SteamId!,
                    FriendSinceUtc = FromUnixSeconds(x.FriendSince)
                })
                .ToList()
        };
    }

    public async Task<SteamPlayerAchievementsResultModel> GetPlayerAchievementsAsync(
        string steamId,
        int appId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ValidateSteamId(steamId);

        if (appId < 1)
            throw new ArgumentOutOfRangeException(nameof(appId));

        var url = BuildUrl(
            "ISteamUserStats/GetPlayerAchievements/v0001/",
            new Dictionary<string, string?>
            {
                ["key"] = _settings.ApiKey,
                ["steamid"] = steamId,
                ["appid"] = appId.ToString(),
                ["l"] = _settings.Language,
                ["format"] = "json"
            });

        using var response = await SendWithRetryAsync(url, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new SteamPlayerAchievementsResultModel { IsVisible = false };

        await EnsureSuccessAsync(response, "Steam nailiyyətləri", cancellationToken);
        var payload = await DeserializeAsync<AchievementsEnvelope>(response, cancellationToken);
        var stats = payload?.PlayerStats;

        if (stats?.Success != true)
            return new SteamPlayerAchievementsResultModel { IsVisible = false };

        return new SteamPlayerAchievementsResultModel
        {
            IsVisible = true,
            GameName = stats.GameName,
            Achievements = stats.Achievements?
                .Where(x => !string.IsNullOrWhiteSpace(x.ApiName))
                .Select(x => new SteamAchievementModel
                {
                    ApiName = x.ApiName!,
                    Achieved = x.Achieved == 1,
                    UnlockedAtUtc = FromUnixSeconds(x.UnlockTime)
                })
                .ToList() ?? []
        };
    }

    public async Task<IReadOnlyDictionary<string, SteamAchievementSchemaModel>> GetAchievementSchemaAsync(
        int appId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        if (appId < 1)
            throw new ArgumentOutOfRangeException(nameof(appId));

        var cacheKey = $"steam:achievement-schema:{appId}:{_settings.Language}";

        var cached = await _cache.GetAsync<Dictionary<string, SteamAchievementSchemaModel>>(
            cacheKey,
            cancellationToken);
        if (cached is not null)
            return cached;

        var url = BuildUrl(
            "ISteamUserStats/GetSchemaForGame/v2/",
            new Dictionary<string, string?>
            {
                ["key"] = _settings.ApiKey,
                ["appid"] = appId.ToString(),
                ["l"] = _settings.Language,
                ["format"] = "json"
            });

        using var response = await SendWithRetryAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, "Steam nailiyyət sxemi", cancellationToken);
        var payload = await DeserializeAsync<SchemaEnvelope>(response, cancellationToken);

        var result = payload?.Game?.AvailableGameStats?.Achievements?
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => new SteamAchievementSchemaModel
            {
                ApiName = x.Name!,
                DisplayName = string.IsNullOrWhiteSpace(x.DisplayName) ? x.Name! : x.DisplayName!,
                Description = x.Description,
                IconUrl = x.Icon,
                GrayIconUrl = x.IconGray
            })
            .ToDictionary(x => x.ApiName, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, SteamAchievementSchemaModel>(StringComparer.OrdinalIgnoreCase);

        await _cache.SetAsync(
            cacheKey,
            result,
            TimeSpan.FromMinutes(_settings.AchievementSchemaCacheMinutes),
            cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<SteamPlayerProfileModel>> GetPlayerSummariesCoreAsync(
        IReadOnlyCollection<string> steamIds,
        CancellationToken cancellationToken)
    {
        if (steamIds.Count == 0)
            return [];

        var url = BuildUrl(
            "ISteamUser/GetPlayerSummaries/v0002/",
            new Dictionary<string, string?>
            {
                ["key"] = _settings.ApiKey,
                ["steamids"] = string.Join(',', steamIds),
                ["format"] = "json"
            });

        using var response = await SendWithRetryAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, "Steam profil məlumatı", cancellationToken);
        var payload = await DeserializeAsync<PlayerSummariesEnvelope>(response, cancellationToken);

        return payload?.Response?.Players?
            .Where(x => !string.IsNullOrWhiteSpace(x.SteamId))
            .Select(MapProfile)
            .ToList() ?? [];
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt <= _settings.RetryCount; attempt++)
        {
            try
            {
                using var lease = await _requestThrottle.EnterAsync(cancellationToken);
                var response = await _httpClient.GetAsync(
                    relativeUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!ShouldRetry(response.StatusCode) || attempt == _settings.RetryCount)
                    return response;

                var delay = GetRetryDelay(response, attempt);
                response.Dispose();
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception exception) when (
                (exception is HttpRequestException or TaskCanceledException) &&
                !cancellationToken.IsCancellationRequested)
            {
                lastException = exception;

                if (attempt == _settings.RetryCount)
                {
                    throw new ExternalServiceException(
                        "Steam API sorğusu tamamlanmadı.",
                        exception);
                }

                await Task.Delay(GetRetryDelay(null, attempt), cancellationToken);
            }
        }

        throw new ExternalServiceException(
            "Steam API sorğusu tamamlanmadı.",
            lastException ?? new HttpRequestException("Steam API sorğusu uğursuz oldu."));
    }

    private async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new ExternalServiceException(
            $"{operation} alınarkən Steam API {((int)response.StatusCode)} statusu qaytardı. " +
            $"Cavab: {TrimForError(body)}");
    }

    private async Task<T?> DeserializeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<T>(
                stream,
                _jsonOptions,
                cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new ExternalServiceException(
                "Steam API cavabı JSON formatında oxuna bilmədi.",
                exception);
        }
    }

    private string BuildUrl(
        string path,
        IReadOnlyDictionary<string, string?> parameters)
    {
        return QueryHelpers.AddQueryString(path, parameters);
    }

    private static SteamPlayerProfileModel MapProfile(PlayerResponse x)
    {
        return new SteamPlayerProfileModel
        {
            SteamId = x.SteamId!,
            PersonaName = x.PersonaName ?? string.Empty,
            ProfileUrl = x.ProfileUrl,
            AvatarUrl = x.Avatar,
            AvatarMediumUrl = x.AvatarMedium,
            AvatarFullUrl = x.AvatarFull,
            PersonaState = x.PersonaState,
            CommunityVisibilityState = x.CommunityVisibilityState,
            LastLogoffUnix = x.LastLogoff,
            GameAppId = int.TryParse(x.GameId, out var gameAppId) ? gameAppId : null,
            GameName = x.GameExtraInfo
        };
    }

    private static SteamOwnedGameModel MapOwnedGame(OwnedGameResponse x)
    {
        return new SteamOwnedGameModel
        {
            AppId = x.AppId,
            Name = x.Name ?? $"Steam App {x.AppId}",
            PlaytimeForeverMinutes = Math.Max(0, x.PlaytimeForever),
            PlaytimeWindowsMinutes = Math.Max(0, x.PlaytimeWindowsForever),
            PlaytimeMacMinutes = Math.Max(0, x.PlaytimeMacForever),
            PlaytimeLinuxMinutes = Math.Max(0, x.PlaytimeLinuxForever),
            PlaytimeTwoWeeksMinutes = x.PlaytimeTwoWeeks,
            LastPlayedUnix = x.LastPlayed,
            IconHash = x.IconHash
        };
    }

    private static DateTime? FromUnixSeconds(long? unixSeconds)
    {
        if (!unixSeconds.HasValue || unixSeconds <= 0)
            return null;

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.TooManyRequests ||
               (int)statusCode >= 500;
    }

    private static TimeSpan GetRetryDelay(
        HttpResponseMessage? response,
        int attempt)
    {
        var retryAfter = response?.Headers.RetryAfter?.Delta;
        return retryAfter ?? TimeSpan.FromMilliseconds(300 * Math.Pow(2, attempt));
    }

    private void EnsureEnabled()
    {
        if (!_settings.Enabled)
        {
            throw new ExternalServiceException(
                "Steam inteqrasiyası deaktivdir. Steam:Enabled konfiqurasiyasını yoxlayın.");
        }
    }

    private static void ValidateSteamId(string steamId)
    {
        if (steamId.Length != 17 || !ulong.TryParse(steamId, out _))
            throw new ArgumentException("Steam ID düzgün deyil.", nameof(steamId));
    }

    private static string TrimForError(string value)
    {
        const int maximumLength = 300;
        var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength];
    }

    private sealed class PlayerSummariesEnvelope
    {
        [JsonPropertyName("response")]
        public PlayerSummariesData? Response { get; init; }
    }

    private sealed class PlayerSummariesData
    {
        [JsonPropertyName("players")]
        public List<PlayerResponse>? Players { get; init; }
    }

    private sealed class PlayerResponse
    {
        [JsonPropertyName("steamid")]
        public string? SteamId { get; init; }

        [JsonPropertyName("personaname")]
        public string? PersonaName { get; init; }

        [JsonPropertyName("profileurl")]
        public string? ProfileUrl { get; init; }

        [JsonPropertyName("avatar")]
        public string? Avatar { get; init; }

        [JsonPropertyName("avatarmedium")]
        public string? AvatarMedium { get; init; }

        [JsonPropertyName("avatarfull")]
        public string? AvatarFull { get; init; }

        [JsonPropertyName("personastate")]
        public int PersonaState { get; init; }

        [JsonPropertyName("communityvisibilitystate")]
        public int CommunityVisibilityState { get; init; }

        [JsonPropertyName("lastlogoff")]
        public long? LastLogoff { get; init; }

        [JsonPropertyName("gameid")]
        public string? GameId { get; init; }

        [JsonPropertyName("gameextrainfo")]
        public string? GameExtraInfo { get; init; }
    }

    private sealed class OwnedGamesEnvelope
    {
        [JsonPropertyName("response")]
        public OwnedGamesData? Response { get; init; }
    }

    private sealed class OwnedGamesData
    {
        [JsonPropertyName("game_count")]
        public int? GameCount { get; init; }

        [JsonPropertyName("games")]
        public List<OwnedGameResponse>? Games { get; init; }
    }

    private sealed class OwnedGameResponse
    {
        [JsonPropertyName("appid")]
        public int AppId { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("playtime_forever")]
        public int PlaytimeForever { get; init; }

        [JsonPropertyName("playtime_windows_forever")]
        public int PlaytimeWindowsForever { get; init; }

        [JsonPropertyName("playtime_mac_forever")]
        public int PlaytimeMacForever { get; init; }

        [JsonPropertyName("playtime_linux_forever")]
        public int PlaytimeLinuxForever { get; init; }

        [JsonPropertyName("playtime_2weeks")]
        public int? PlaytimeTwoWeeks { get; init; }

        [JsonPropertyName("rtime_last_played")]
        public long? LastPlayed { get; init; }

        [JsonPropertyName("img_icon_url")]
        public string? IconHash { get; init; }
    }

    private sealed class FriendsEnvelope
    {
        [JsonPropertyName("friendslist")]
        public FriendsListData? FriendsList { get; init; }
    }

    private sealed class FriendsListData
    {
        [JsonPropertyName("friends")]
        public List<FriendResponse>? Friends { get; init; }
    }

    private sealed class FriendResponse
    {
        [JsonPropertyName("steamid")]
        public string? SteamId { get; init; }

        [JsonPropertyName("friend_since")]
        public long? FriendSince { get; init; }
    }

    private sealed class AchievementsEnvelope
    {
        [JsonPropertyName("playerstats")]
        public PlayerStatsData? PlayerStats { get; init; }
    }

    private sealed class PlayerStatsData
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("gameName")]
        public string? GameName { get; init; }

        [JsonPropertyName("achievements")]
        public List<AchievementResponse>? Achievements { get; init; }
    }

    private sealed class AchievementResponse
    {
        [JsonPropertyName("apiname")]
        public string? ApiName { get; init; }

        [JsonPropertyName("achieved")]
        public int Achieved { get; init; }

        [JsonPropertyName("unlocktime")]
        public long? UnlockTime { get; init; }
    }

    private sealed class SchemaEnvelope
    {
        [JsonPropertyName("game")]
        public SchemaGameData? Game { get; init; }
    }

    private sealed class SchemaGameData
    {
        [JsonPropertyName("availableGameStats")]
        public AvailableGameStatsData? AvailableGameStats { get; init; }
    }

    private sealed class AvailableGameStatsData
    {
        [JsonPropertyName("achievements")]
        public List<SchemaAchievementResponse>? Achievements { get; init; }
    }

    private sealed class SchemaAchievementResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("icon")]
        public string? Icon { get; init; }

        [JsonPropertyName("icongray")]
        public string? IconGray { get; init; }
    }
}
