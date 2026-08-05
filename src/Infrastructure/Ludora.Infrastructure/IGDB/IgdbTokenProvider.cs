using System.Net.Http.Json;
using System.Text.Json;
using Ludora.Application.Exceptions;
using Ludora.Infrastructure.IGDB.Models;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.IGDB;

public sealed class IgdbTokenProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IgdbSettings _settings;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _accessToken;
    private long _expiresAtUnixSeconds;

    public IgdbTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<IgdbSettings> options)
    {
        _httpClientFactory = httpClientFactory;
        _settings = options.Value;
    }

    public async Task<string> GetAccessTokenAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        if (HasValidToken())
            return _accessToken!;

        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (HasValidToken())
                return _accessToken!;

            var tokenUrl =
                $"{_settings.TokenUrl}" +
                $"?client_id={Uri.EscapeDataString(_settings.ClientId.Trim())}" +
                $"&client_secret={Uri.EscapeDataString(_settings.ClientSecret.Trim())}" +
                "&grant_type=client_credentials";

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    tokenUrl);

            var client = _httpClientFactory.CreateClient("IgdbToken");

            try
            {
                using var response = await client.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    var technicalException = new HttpRequestException(
                        $"Twitch token endpoint returned {(int)response.StatusCode}: {responseBody}",
                        null,
                        response.StatusCode);

                    throw new ExternalServiceException(
                        "IGDB üçün giriş tokeni alına bilmədi.",
                        technicalException);
                }

                var token = await response.Content.ReadFromJsonAsync<TwitchTokenResponse>(
                    cancellationToken: cancellationToken);

                if (token is null ||
                    string.IsNullOrWhiteSpace(token.AccessToken) ||
                    token.ExpiresIn <= 0)
                {
                    throw new ExternalServiceException(
                        "IGDB token cavabı düzgün formatda deyil.");
                }

                var safetyWindowSeconds = Math.Min(60, Math.Max(1, token.ExpiresIn / 10));

                _accessToken = token.AccessToken;
                Interlocked.Exchange(
                    ref _expiresAtUnixSeconds,
                    DateTimeOffset.UtcNow
                        .AddSeconds(Math.Max(1, token.ExpiresIn - safetyWindowSeconds))
                        .ToUnixTimeSeconds());

                return _accessToken;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ExternalServiceException(
                    "IGDB token sorğusunun vaxtı bitdi.");
            }
            catch (HttpRequestException exception)
            {
                throw new ExternalServiceException(
                    "IGDB token xidmətinə qoşulmaq mümkün olmadı.",
                    exception);
            }
            catch (JsonException exception)
            {
                throw new ExternalServiceException(
                    "IGDB token cavabı oxuna bilmədi.",
                    exception);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public void InvalidateToken()
    {
        _accessToken = null;
        Interlocked.Exchange(ref _expiresAtUnixSeconds, 0);
    }

    private bool HasValidToken()
    {
        return !string.IsNullOrWhiteSpace(_accessToken) &&
               Volatile.Read(ref _expiresAtUnixSeconds) >
               DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private void EnsureEnabled()
    {
        if (!_settings.Enabled)
        {
            throw new ExternalServiceException(
                "IGDB inteqrasiyası aktiv deyil.");
        }
    }
}
