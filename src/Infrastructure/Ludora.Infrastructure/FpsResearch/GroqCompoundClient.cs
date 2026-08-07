using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.FpsResearch
{
    public sealed class GroqCompoundClient : IFpsResearchClient
    {
        private const string InvalidResponseMessage =
            "FPS benchmark xidməti düzgün cavab qaytarmadı. Bir qədər sonra yenidən yoxlayın.";

        private static readonly HashSet<HttpStatusCode> RetryableStatusCodes =
        [
            HttpStatusCode.RequestTimeout,
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.GatewayTimeout
        ];

        private static readonly Regex UrlRegex = new(
            @"https?://[^\s""'<>\]\)]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly HttpClient _httpClient;
        private readonly FpsResearchSettings _settings;
        private readonly ILogger<GroqCompoundClient> _logger;

        public GroqCompoundClient(
            HttpClient httpClient,
            IOptions<FpsResearchSettings> options,
            ILogger<GroqCompoundClient> logger)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _logger = logger;
        }

        public bool IsEnabled => _settings.Enabled;

        public async Task<FpsResearchCompletion> ResearchAsync(
            string systemPrompt,
            string benchmarkSearchPrompt,
            CancellationToken cancellationToken = default,
            string? modelOverride = null,
            string? modelVersionOverride = null)
        {
            if (!IsEnabled)
            {
                throw new ExternalServiceException(
                    "FPS benchmark xidməti aktiv deyil.");
            }

            var model = string.IsNullOrWhiteSpace(modelOverride)
                ? _settings.Model
                : modelOverride.Trim();
            var modelVersion = string.IsNullOrWhiteSpace(modelVersionOverride)
                ? _settings.ModelVersion
                : modelVersionOverride.Trim();

            var isCompoundSystem = model.StartsWith(
                "groq/compound",
                StringComparison.OrdinalIgnoreCase);

            // FPS has three provider paths: Compound Mini, Compound and finally a
            // plain production model. Only Compound systems accept compound_custom.
            // The previous implementation always sent compound_custom, which made a
            // non-Compound fallback impossible and left the endpoint dependent on a
            // single provider response shape.
            var payloadData = new Dictionary<string, object?>
            {
                ["model"] = model,
                ["temperature"] = 0.1,
                ["max_completion_tokens"] = isCompoundSystem ? 3500 : 2400,
                ["response_format"] = new
                {
                    type = "json_object"
                },
                ["messages"] = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = benchmarkSearchPrompt }
                }
            };

            if (isCompoundSystem)
            {
                payloadData["compound_custom"] = new
                {
                    tools = new
                    {
                        enabled_tools = new[]
                        {
                            "web_search"
                        }
                    }
                };
            }
            else if (model.StartsWith(
                         "openai/gpt-oss",
                         StringComparison.OrdinalIgnoreCase))
            {
                // Keep the emergency JSON-only fallback quick and predictable.
                payloadData["reasoning_effort"] = "low";
            }

            var payload = JsonSerializer.Serialize(payloadData);

            for (var attempt = 0; attempt <= _settings.MaxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var startedAt = Stopwatch.GetTimestamp();

                try
                {
                    using var request = CreateRequest(
                        payload,
                        isCompoundSystem ? modelVersion : string.Empty);
                    using var response = await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);

                    var elapsed = Stopwatch.GetElapsedTime(startedAt);
                    var correlationId =
                        Activity.Current?.TraceId.ToString() ?? "none";

                    if (response.IsSuccessStatusCode)
                    {
                        var responseJson = await response.Content
                            .ReadAsStringAsync(cancellationToken);

                        _logger.LogInformation(
                            "Groq Compound request uğurlu oldu. Model {Model}, DurationMs {DurationMs}, StatusCode {StatusCode}, RetryCount {RetryCount}, CorrelationId {CorrelationId}.",
                            model,
                            elapsed.TotalMilliseconds,
                            (int)response.StatusCode,
                            attempt,
                            correlationId);

                        var completion = ReadCompletion(responseJson);

                        _logger.LogInformation(
                            "Groq Compound cavabında {SourceCount} research source tapıldı. Model {Model}, CorrelationId {CorrelationId}.",
                            completion.Sources.Count,
                            model,
                            correlationId);

                        return completion;
                    }

                    var errorBody = await response.Content
                        .ReadAsStringAsync(cancellationToken);
                    var providerError = ReadProviderError(errorBody);
                    var retryAfterSeconds = GetRetryAfterSeconds(response);

                    _logger.LogWarning(
                        "Groq Compound request uğursuz oldu. Model {Model}, DurationMs {DurationMs}, StatusCode {StatusCode}, RetryCount {RetryCount}, ProviderErrorCode {ProviderErrorCode}, ProviderErrorType {ProviderErrorType}, ProviderMessage {ProviderMessage}, RetryAfterSeconds {RetryAfterSeconds}, RemainingRequests {RemainingRequests}, RemainingTokens {RemainingTokens}, CorrelationId {CorrelationId}.",
                        model,
                        elapsed.TotalMilliseconds,
                        (int)response.StatusCode,
                        attempt,
                        providerError.Code,
                        providerError.Type,
                        providerError.Message,
                        retryAfterSeconds,
                        GetHeader(response, "x-ratelimit-remaining-requests"),
                        GetHeader(response, "x-ratelimit-remaining-tokens"),
                        correlationId);

                    var retryDelay = GetRetryDelay(
                        response,
                        attempt,
                        retryAfterSeconds);

                    if (RetryableStatusCodes.Contains(response.StatusCode) &&
                        attempt < _settings.MaxRetries &&
                        retryDelay.HasValue)
                    {
                        await Task.Delay(retryDelay.Value, cancellationToken);
                        continue;
                    }

                    throw CreateProviderException(
                        response.StatusCode,
                        retryAfterSeconds);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (TaskCanceledException exception)
                {
                    throw new ExternalServiceException(
                        "FPS benchmark axtarışının cavab müddəti bitdi. Bir qədər sonra yenidən yoxlayın.",
                        exception);
                }
                catch (HttpRequestException exception)
                {
                    throw new ExternalServiceException(
                        "FPS benchmark xidməti ilə əlaqə yaratmaq mümkün olmadı.",
                        exception);
                }
            }

            throw new ExternalServiceException(
                "FPS benchmark xidməti hazırda əlçatan deyil. Bir qədər sonra yenidən yoxlayın.");
        }

        private HttpRequestMessage CreateRequest(
            string payload,
            string modelVersion)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "chat/completions");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrWhiteSpace(modelVersion))
            {
                request.Headers.TryAddWithoutValidation(
                    "Groq-Model-Version",
                    modelVersion);
            }

            request.Content = new StringContent(
                payload,
                Encoding.UTF8,
                "application/json");

            return request;
        }

        private static ExternalServiceException CreateProviderException(
            HttpStatusCode statusCode,
            int? retryAfterSeconds)
        {
            if (statusCode == HttpStatusCode.TooManyRequests)
            {
                return new ExternalServiceException(
                    "AI xidmətinin sorğu limiti müvəqqəti dolub. Bir qədər sonra yenidən yoxlayın.",
                    429,
                    retryAfterSeconds);
            }

            if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new ExternalServiceException(
                    "AI xidməti hazırda autentifikasiya oluna bilmir. Bir qədər sonra yenidən yoxlayın.");
            }

            if (statusCode == HttpStatusCode.BadRequest)
            {
                return new ExternalServiceException(
                    "AI xidməti sorğunu qəbul etmədi. Bir qədər sonra yenidən yoxlayın.");
            }

            return new ExternalServiceException(
                "FPS benchmark xidməti hazırda əlçatan deyil. Bir qədər sonra yenidən yoxlayın.");
        }

        private static FpsResearchCompletion ReadCompletion(
            string responseJson)
        {
            try
            {
                using var document = JsonDocument.Parse(responseJson);
                var message = document.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message");
                var content = message
                    .GetProperty("content")
                    .GetString();

                if (string.IsNullOrWhiteSpace(content))
                    throw new JsonException("Groq content boşdur.");

                content = NormalizeJsonContent(content);

                var sources = new List<RankedResearchSource>();

                if (message.TryGetProperty(
                        "executed_tools",
                        out var executedTools) &&
                    executedTools.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tool in executedTools.EnumerateArray())
                        CollectSources(tool, sources);
                }

                return new FpsResearchCompletion
                {
                    Content = content,
                    Sources = sources
                        .DistinctBy(
                            source => source.Url,
                            StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(source => source.RelevanceScore)
                        .Take(8)
                        .Select(source => source.Source)
                        .ToList()
                };
            }
            catch (Exception exception)
                when (exception is JsonException or
                      InvalidOperationException or
                      KeyNotFoundException)
            {
                throw new ExternalServiceException(
                    InvalidResponseMessage,
                    exception);
            }
        }

        private static string NormalizeJsonContent(string content)
        {
            var normalized = content.Trim();

            if (normalized.StartsWith("```", StringComparison.Ordinal))
            {
                var firstLineEnd = normalized.IndexOf('\n');
                var closingFence = normalized.LastIndexOf(
                    "```",
                    StringComparison.Ordinal);

                if (firstLineEnd >= 0 && closingFence > firstLineEnd)
                {
                    normalized = normalized[
                        (firstLineEnd + 1)..closingFence].Trim();
                }
            }

            var firstObjectBrace = normalized.IndexOf('{');
            var lastObjectBrace = normalized.LastIndexOf('}');

            if (firstObjectBrace > 0 ||
                lastObjectBrace < normalized.Length - 1)
            {
                if (firstObjectBrace < 0 ||
                    lastObjectBrace <= firstObjectBrace)
                {
                    throw new JsonException(
                        "Groq cavabında JSON object tapılmadı.");
                }

                normalized = normalized[
                    firstObjectBrace..(lastObjectBrace + 1)];
            }

            using var document = JsonDocument.Parse(normalized);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Groq cavabı JSON object deyil.");

            return normalized;
        }

        private static void CollectSources(
            JsonElement element,
            ICollection<RankedResearchSource> sources)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    if (element.TryGetProperty("url", out var urlElement) &&
                        urlElement.ValueKind == JsonValueKind.String)
                    {
                        var urlValue = urlElement.GetString();
                        var title = element.TryGetProperty("title", out var titleElement) &&
                                    titleElement.ValueKind == JsonValueKind.String
                            ? titleElement.GetString()
                            : null;
                        var score = element.TryGetProperty("score", out var scoreElement) &&
                                    scoreElement.TryGetDouble(out var parsedScore)
                            ? parsedScore
                            : 0;

                        AddSource(urlValue, title, score, sources);
                    }

                    foreach (var property in element.EnumerateObject())
                        CollectSources(property.Value, sources);
                    break;
                }
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                        CollectSources(item, sources);
                    break;
                case JsonValueKind.String:
                {
                    var value = element.GetString();
                    if (string.IsNullOrWhiteSpace(value))
                        break;

                    foreach (Match match in UrlRegex.Matches(value))
                        AddSource(match.Value, null, 0, sources);
                    break;
                }
            }
        }

        private static void AddSource(
            string? urlValue,
            string? title,
            double score,
            ICollection<RankedResearchSource> sources)
        {
            if (!TryNormalizeUrl(urlValue, out var normalizedUrl))
                return;

            sources.Add(new RankedResearchSource
            {
                Source = new FpsResearchSource
                {
                    Title = string.IsNullOrWhiteSpace(title)
                        ? new Uri(normalizedUrl).Host
                        : title.Trim(),
                    Url = normalizedUrl
                },
                RelevanceScore = score
            });
        }

        private static bool TryNormalizeUrl(
            string? value,
            out string normalizedUrl)
        {
            normalizedUrl = string.Empty;
            var candidate = value?.Trim().TrimEnd('.', ',', ';', ':', '!', '?');

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps &&
                 uri.Scheme != Uri.UriSchemeHttp))
            {
                return false;
            }

            normalizedUrl = uri.AbsoluteUri;
            return true;
        }

        private static TimeSpan? GetRetryDelay(
            HttpResponseMessage response,
            int attempt,
            int? retryAfterSeconds)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (retryAfterSeconds is > 15)
                    return null;

                if (retryAfterSeconds is > 0)
                    return TimeSpan.FromSeconds(retryAfterSeconds.Value);

                return TimeSpan.FromSeconds(1);
            }

            return TimeSpan.FromMilliseconds(
                500 * Math.Pow(2, attempt));
        }

        private static int? GetRetryAfterSeconds(HttpResponseMessage response)
        {
            if (response.Headers.RetryAfter?.Delta is { } delta &&
                delta > TimeSpan.Zero)
            {
                return Math.Max(1, (int)Math.Ceiling(delta.TotalSeconds));
            }

            if (response.Headers.RetryAfter?.Date is { } retryDate)
            {
                var remaining = retryDate - DateTimeOffset.UtcNow;
                if (remaining > TimeSpan.Zero)
                    return Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
            }

            return null;
        }

        private static string? GetHeader(
            HttpResponseMessage response,
            string headerName)
        {
            return response.Headers.TryGetValues(headerName, out var values)
                ? values.FirstOrDefault()
                : null;
        }

        private static ProviderError ReadProviderError(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return new ProviderError(null, null, null);

            try
            {
                using var document = JsonDocument.Parse(responseBody);
                if (!document.RootElement.TryGetProperty("error", out var error))
                {
                    return new ProviderError(
                        LimitForLog(responseBody),
                        null,
                        null);
                }

                var message = error.TryGetProperty("message", out var messageElement)
                    ? messageElement.ToString()
                    : null;
                var type = error.TryGetProperty("type", out var typeElement)
                    ? typeElement.ToString()
                    : null;
                var code = error.TryGetProperty("code", out var codeElement)
                    ? codeElement.ToString()
                    : null;

                return new ProviderError(
                    LimitForLog(message),
                    LimitForLog(type),
                    LimitForLog(code));
            }
            catch (JsonException)
            {
                return new ProviderError(
                    LimitForLog(responseBody),
                    null,
                    null);
            }
        }

        private static string? LimitForLog(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return normalized.Length <= 600
                ? normalized
                : normalized[..600] + "…";
        }

        private sealed record ProviderError(
            string? Message,
            string? Type,
            string? Code);

        private sealed class RankedResearchSource
        {
            public FpsResearchSource Source { get; init; } = new();
            public string Title => Source.Title;
            public string Url => Source.Url;
            public double RelevanceScore { get; init; }
        }
    }
}
