using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.FpsResearch;

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
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            throw new ExternalServiceException(
                "FPS benchmark xidməti aktiv deyil.");
        }

        var payload = JsonSerializer.Serialize(new
        {
            model = _settings.Model,
            temperature = 0.1,
            max_tokens = 1600,
            response_format = new
            {
                type = "json_object"
            },
            compound_custom = new
            {
                tools = new
                {
                    enabled_tools = new[]
                    {
                        "web_search"
                    }
                }
            },
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = benchmarkSearchPrompt }
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
                var correlationId =
                    Activity.Current?.TraceId.ToString() ?? "none";

                _logger.LogInformation(
                    "FPS benchmark research tamamlandı. Provider Groq Compound, Model {Model}, DurationMs {DurationMs}, StatusCode {StatusCode}, RetryCount {RetryCount}, CorrelationId {CorrelationId}.",
                    _settings.Model,
                    elapsed.TotalMilliseconds,
                    (int)response.StatusCode,
                    attempt,
                    correlationId);

                if (RetryableStatusCodes.Contains(response.StatusCode) &&
                    attempt < _settings.MaxRetries)
                {
                    await Task.Delay(
                        GetRetryDelay(response, attempt),
                        cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new ExternalServiceException(
                        "FPS benchmark xidməti hazırda əlçatan deyil. Bir qədər sonra yenidən yoxlayın.");
                }

                var responseJson = await response.Content
                    .ReadAsStringAsync(cancellationToken);

                return ReadCompletion(responseJson);
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

    private HttpRequestMessage CreateRequest(string payload)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "chat/completions");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation(
            "Groq-Model-Version",
            _settings.ModelVersion);
        request.Content = new StringContent(
            payload,
            Encoding.UTF8,
            "application/json");

        return request;
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
                    ReadToolSearchResults(tool, sources);
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

    private static void ReadToolSearchResults(
        JsonElement tool,
        ICollection<RankedResearchSource> sources)
    {
        if (!tool.TryGetProperty(
                "search_results",
                out var searchResults))
        {
            return;
        }

        if (searchResults.ValueKind == JsonValueKind.Object &&
            searchResults.TryGetProperty(
                "results",
                out var nestedResults))
        {
            searchResults = nestedResults;
        }

        if (searchResults.ValueKind != JsonValueKind.Array)
            return;

        foreach (var result in searchResults.EnumerateArray())
        {
            var urlValue = result.TryGetProperty("url", out var url)
                ? url.GetString()
                : null;

            if (!TryNormalizeUrl(urlValue, out var normalizedUrl))
                continue;

            var title = result.TryGetProperty("title", out var titleValue)
                ? titleValue.GetString()
                : null;
            var score =
                result.TryGetProperty("score", out var scoreValue) &&
                scoreValue.TryGetDouble(out var parsedScore)
                    ? parsedScore
                    : 0;

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
    }

    private static bool TryNormalizeUrl(
        string? value,
        out string normalizedUrl)
    {
        normalizedUrl = string.Empty;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
             uri.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        normalizedUrl = uri.AbsoluteUri;
        return true;
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

        return TimeSpan.FromMilliseconds(
            350 * Math.Pow(2, attempt));
    }

    private sealed class RankedResearchSource
    {
        public FpsResearchSource Source { get; init; } = new();
        public string Title => Source.Title;
        public string Url => Source.Url;
        public double RelevanceScore { get; init; }
    }
}
