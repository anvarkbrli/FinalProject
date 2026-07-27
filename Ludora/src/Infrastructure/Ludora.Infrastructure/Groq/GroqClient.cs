using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.Groq;

public sealed class GroqClient : IGroqClient
{
    private const string InvalidResponseMessage =
        "FPS təxmin xidməti düzgün cavab qaytarmadı. Bir qədər sonra yenidən yoxlayın.";

    private static readonly HashSet<HttpStatusCode> RetryableStatusCodes =
    [
        HttpStatusCode.RequestTimeout,
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout
    ];

    private readonly HttpClient _httpClient;
    private readonly GroqSettings _settings;
    private readonly ILogger<GroqClient> _logger;

    public GroqClient(
        HttpClient httpClient,
        IOptions<GroqSettings> options,
        ILogger<GroqClient> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => _settings.Enabled;

    public async Task<string> GetJsonCompletionAsync(
        string systemPrompt,
        string userPayloadJson,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            throw new ExternalServiceException("FPS təxmin xidməti aktiv deyil.");

        var payload = JsonSerializer.Serialize(new
        {
            model = _settings.Model,
            temperature = 0.2,
            response_format = new
            {
                type = "json_object"
            },
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = systemPrompt
                },
                new
                {
                    role = "user",
                    content = userPayloadJson
                }
            }
        });

        for (var attempt = 0; attempt <= _settings.MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var startedAt = Stopwatch.GetTimestamp();

            try
            {
                using var request = CreateRequest(payload);
                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                var elapsed = Stopwatch.GetElapsedTime(startedAt);
                var correlationId = Activity.Current?.TraceId.ToString() ?? "none";

                _logger.LogInformation(
                    "Groq request tamamlandı. Model {Model}, DurationMs {DurationMs}, StatusCode {StatusCode}, RetryCount {RetryCount}, CorrelationId {CorrelationId}.",
                    _settings.Model,
                    elapsed.TotalMilliseconds,
                    (int)response.StatusCode,
                    attempt,
                    correlationId);

                if (RetryableStatusCodes.Contains(response.StatusCode) &&
                    attempt < _settings.MaxRetries)
                {
                    var delay = GetRetryDelay(response, attempt);

                    _logger.LogWarning(
                        "Groq request təkrarlanacaq. Model {Model}, StatusCode {StatusCode}, RetryCount {RetryCount}, CorrelationId {CorrelationId}.",
                        _settings.Model,
                        (int)response.StatusCode,
                        attempt + 1,
                        correlationId);

                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new ExternalServiceException(
                        "FPS təxmin xidməti hazırda əlçatan deyil. Bir qədər sonra yenidən yoxlayın.");
                }

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                return ReadCompletionContent(responseJson);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException exception)
            {
                throw new ExternalServiceException(
                    "FPS təxmin xidmətinin cavab müddəti bitdi. Bir qədər sonra yenidən yoxlayın.",
                    exception);
            }
            catch (HttpRequestException exception)
            {
                throw new ExternalServiceException(
                    "FPS təxmin xidməti ilə əlaqə yaratmaq mümkün olmadı.",
                    exception);
            }
        }

        throw new ExternalServiceException(
            "FPS təxmin xidməti hazırda əlçatan deyil. Bir qədər sonra yenidən yoxlayın.");
    }

    private HttpRequestMessage CreateRequest(string payload)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "chat/completions");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(
            payload,
            Encoding.UTF8,
            "application/json");

        return request;
    }

    private static string ReadCompletionContent(string responseJson)
    {
        try
        {
            var response = JsonSerializer.Deserialize<GroqCompletionResponse>(
                responseJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            var content = response?.Choices?
                .FirstOrDefault()?
                .Message?
                .Content;

            if (string.IsNullOrWhiteSpace(content))
                throw new JsonException("Groq content boşdur.");

            return content;
        }
        catch (JsonException exception)
        {
            throw new ExternalServiceException(
                InvalidResponseMessage,
                exception);
        }
    }

    private static TimeSpan GetRetryDelay(
        HttpResponseMessage response,
        int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta &&
            delta > TimeSpan.Zero &&
            delta <= TimeSpan.FromSeconds(10))
        {
            return delta;
        }

        return TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt));
    }

    private sealed class GroqCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<GroqChoice>? Choices { get; init; }
    }

    private sealed class GroqChoice
    {
        [JsonPropertyName("message")]
        public GroqMessage? Message { get; init; }
    }

    private sealed class GroqMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }
}
