using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models;
using Ludora.Domain.Enums;
using Ludora.Infrastructure.IGDB.Models;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.IGDB;

public sealed class IgdbClient : IIgdbClient
{
    private const int MaximumIdsPerRequest = 500;
    private readonly HttpClient _httpClient;
    private readonly IgdbTokenProvider _tokenProvider;
    private readonly IgdbRequestThrottle _requestThrottle;
    private readonly IgdbSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IgdbClient(
        HttpClient httpClient,
        IgdbTokenProvider tokenProvider,
        IgdbRequestThrottle requestThrottle,
        IOptions<IgdbSettings> options)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _requestThrottle = requestThrottle;
        _settings = options.Value;
    }

    public bool IsEnabled => _settings.Enabled;

    public async Task<IReadOnlyList<IgdbGameModel>> GetGamesAsync(
        GameFilterDTO filter,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        ArgumentNullException.ThrowIfNull(filter);

        var responses = await SendAsync<IgdbGameResponse>(
            "games",
            IgdbQueryBuilder.BuildGames(filter),
            cancellationToken);

        return responses
            .Where(IsValidNamedResponse)
            .Select(MapGame)
            .ToList();
    }

    public async Task<IgdbGameModel?> GetGameByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id < 1)
            return null;

        var games = await GetGamesByIdsAsync([id], cancellationToken);
        return games.FirstOrDefault();
    }

    public async Task<IReadOnlyList<IgdbGameModel>> GetGamesByIdsAsync(
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        return await GetByIdsInBatchesAsync<IgdbGameResponse, IgdbGameModel>(
            "games",
            ids,
            IgdbQueryBuilder.BuildGamesByIds,
            MapGame,
            IsValidNamedResponse,
            cancellationToken);
    }

    public async Task<IReadOnlyList<IgdbGenreModel>> GetGenresAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        var responses = await SendAsync<IgdbGenreResponse>(
            "genres",
            IgdbQueryBuilder.BuildNamedList(search),
            cancellationToken);

        return responses
            .Where(IsValidNamedResponse)
            .Select(MapGenre)
            .ToList();
    }

    public async Task<IgdbGenreModel?> GetGenreByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id < 1)
            return null;

        var result = await GetGenresByIdsAsync([id], cancellationToken);
        return result.FirstOrDefault();
    }

    public async Task<IReadOnlyList<IgdbGenreModel>> GetGenresByIdsAsync(
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        return await GetByIdsInBatchesAsync<IgdbGenreResponse, IgdbGenreModel>(
            "genres",
            ids,
            IgdbQueryBuilder.BuildNamedByIds,
            MapGenre,
            IsValidNamedResponse,
            cancellationToken);
    }

    public async Task<IReadOnlyList<IgdbPlatformModel>> GetPlatformsAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        var responses = await SendAsync<IgdbPlatformResponse>(
            "platforms",
            IgdbQueryBuilder.BuildNamedList(search),
            cancellationToken);

        return responses
            .Where(IsValidNamedResponse)
            .Select(MapPlatform)
            .ToList();
    }

    public async Task<IgdbPlatformModel?> GetPlatformByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id < 1)
            return null;

        var result = await GetPlatformsByIdsAsync([id], cancellationToken);
        return result.FirstOrDefault();
    }

    public async Task<IReadOnlyList<IgdbPlatformModel>> GetPlatformsByIdsAsync(
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        return await GetByIdsInBatchesAsync<IgdbPlatformResponse, IgdbPlatformModel>(
            "platforms",
            ids,
            IgdbQueryBuilder.BuildNamedByIds,
            MapPlatform,
            IsValidNamedResponse,
            cancellationToken);
    }

    public async Task<IReadOnlyList<IgdbCompanyModel>> GetCompaniesAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        var responses = await SendAsync<IgdbCompanyResponse>(
            "companies",
            IgdbQueryBuilder.BuildNamedList(search),
            cancellationToken);

        return responses
            .Where(IsValidNamedResponse)
            .Select(MapCompany)
            .ToList();
    }

    public async Task<IgdbCompanyModel?> GetCompanyByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id < 1)
            return null;

        var result = await GetCompaniesByIdsAsync([id], cancellationToken);
        return result.FirstOrDefault();
    }

    public async Task<IReadOnlyList<IgdbCompanyModel>> GetCompaniesByIdsAsync(
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        return await GetByIdsInBatchesAsync<IgdbCompanyResponse, IgdbCompanyModel>(
            "companies",
            ids,
            IgdbQueryBuilder.BuildNamedByIds,
            MapCompany,
            IsValidNamedResponse,
            cancellationToken);
    }

    private async Task<IReadOnlyList<TModel>> GetByIdsInBatchesAsync<TResponse, TModel>(
        string endpoint,
        IReadOnlyCollection<long> ids,
        Func<IReadOnlyCollection<long>, string> queryFactory,
        Func<TResponse, TModel> mapper,
        Func<TResponse, bool> validator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var normalizedIds = ids
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        if (normalizedIds.Length == 0)
            return Array.Empty<TModel>();

        var result = new List<TModel>(normalizedIds.Length);

        foreach (var batch in normalizedIds.Chunk(MaximumIdsPerRequest))
        {
            var responses = await SendAsync<TResponse>(
                endpoint,
                queryFactory(batch),
                cancellationToken);

            result.AddRange(responses.Where(validator).Select(mapper));
        }

        return result;
    }

    private async Task<IReadOnlyList<T>> SendAsync<T>(
        string endpoint,
        string query,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();

        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var requestLease = await _requestThrottle.EnterAsync(cancellationToken);
                using var response = await SendCoreAsync(endpoint, query, cancellationToken);

                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                {
                    _tokenProvider.InvalidateToken();
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 2)
                {
                    await Task.Delay(GetRetryDelay(response, attempt), cancellationToken);
                    continue;
                }

                if ((int)response.StatusCode >= 500 && attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(300 * (attempt + 1)), cancellationToken);
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var technicalException = new HttpRequestException(
                        $"IGDB endpoint '{endpoint}' returned {(int)response.StatusCode}: {body}",
                        null,
                        response.StatusCode);

                    throw new ExternalServiceException(
                        "IGDB xidmətindən məlumat almaq mümkün olmadı.",
                        technicalException);
                }

                try
                {
                    return JsonSerializer.Deserialize<List<T>>(body, _jsonOptions)
                           ?? new List<T>();
                }
                catch (JsonException exception)
                {
                    throw new ExternalServiceException(
                        "IGDB cavabı oxuna bilmədi.",
                        exception);
                }
            }

            throw new ExternalServiceException(
                "IGDB xidmətindən məlumat almaq mümkün olmadı.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceException(
                "IGDB sorğusunun vaxtı bitdi.");
        }
        catch (HttpRequestException exception)
        {
            throw new ExternalServiceException(
                "IGDB xidmətinə qoşulmaq mümkün olmadı.",
                exception);
        }
    }

    private async Task<HttpResponseMessage> SendCoreAsync(
        string endpoint,
        string query,
        CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.TryAddWithoutValidation("Client-ID", _settings.ClientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(query, Encoding.UTF8, "text/plain");

        return await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
            return delta;

        if (response.Headers.RetryAfter?.Date is { } retryDate)
        {
            var calculatedDelay = retryDate - DateTimeOffset.UtcNow;

            if (calculatedDelay > TimeSpan.Zero)
                return calculatedDelay;
        }

        return TimeSpan.FromSeconds(attempt + 1);
    }

    private static bool IsValidNamedResponse(IgdbGameResponse response)
    {
        return response.Id > 0 && !string.IsNullOrWhiteSpace(response.Name);
    }

    private static bool IsValidNamedResponse(IgdbGenreResponse response)
    {
        return response.Id > 0 && !string.IsNullOrWhiteSpace(response.Name);
    }

    private static bool IsValidNamedResponse(IgdbPlatformResponse response)
    {
        return response.Id > 0 && !string.IsNullOrWhiteSpace(response.Name);
    }

    private static bool IsValidNamedResponse(IgdbCompanyResponse response)
    {
        return response.Id > 0 && !string.IsNullOrWhiteSpace(response.Name);
    }

    private static IgdbGameModel MapGame(IgdbGameResponse response)
    {
        var companies = new List<IgdbCompanyRelationModel>();

        foreach (var involvedCompany in response.InvolvedCompanies ?? [])
        {
            if (involvedCompany.Company is null ||
                !IsValidNamedResponse(involvedCompany.Company))
            {
                continue;
            }

            var company = MapCompany(involvedCompany.Company);

            if (involvedCompany.Developer)
            {
                companies.Add(new IgdbCompanyRelationModel
                {
                    Company = company,
                    Role = GameCompanyRole.Developer
                });
            }

            if (involvedCompany.Publisher)
            {
                companies.Add(new IgdbCompanyRelationModel
                {
                    Company = company,
                    Role = GameCompanyRole.Publisher
                });
            }
        }

        DateOnly? releaseDate = null;

        if (response.FirstReleaseDate.HasValue)
        {
            try
            {
                releaseDate = DateOnly.FromDateTime(
                    DateTimeOffset
                        .FromUnixTimeSeconds(response.FirstReleaseDate.Value)
                        .UtcDateTime);
            }
            catch (ArgumentOutOfRangeException)
            {
                releaseDate = null;
            }
        }

        var coverUrl = string.IsNullOrWhiteSpace(response.Cover?.ImageId)
            ? null
            : $"https://images.igdb.com/igdb/image/upload/t_cover_big/{response.Cover.ImageId}.jpg";

        return new IgdbGameModel
        {
            Id = response.Id,
            Name = response.Name.Trim(),
            Description = response.Summary?.Trim(),
            ReleaseDate = releaseDate,
            CoverImageUrl = coverUrl,
            Rating = response.TotalRating,
            Genres = (response.Genres ?? [])
                .Where(IsValidNamedResponse)
                .Select(MapGenre)
                .DistinctBy(x => x.Id)
                .ToList(),
            Platforms = (response.Platforms ?? [])
                .Where(IsValidNamedResponse)
                .Select(MapPlatform)
                .DistinctBy(x => x.Id)
                .ToList(),
            Companies = companies
                .DistinctBy(x => new { x.Company.Id, x.Role })
                .ToList()
        };
    }

    private static IgdbGenreModel MapGenre(IgdbGenreResponse response)
    {
        return new IgdbGenreModel
        {
            Id = response.Id,
            Name = response.Name.Trim()
        };
    }

    private static IgdbPlatformModel MapPlatform(IgdbPlatformResponse response)
    {
        return new IgdbPlatformModel
        {
            Id = response.Id,
            Name = response.Name.Trim()
        };
    }

    private static IgdbCompanyModel MapCompany(IgdbCompanyResponse response)
    {
        return new IgdbCompanyModel
        {
            Id = response.Id,
            Name = response.Name.Trim()
        };
    }

    private void EnsureEnabled()
    {
        if (!IsEnabled)
        {
            throw new ExternalServiceException(
                "IGDB inteqrasiyası aktiv deyil.");
        }
    }
}
