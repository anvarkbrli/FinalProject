using System.Net.Http.Json;
using System.Text.Json;
using Ludora.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.Translation;

public sealed class AzureTranslationProvider : ITranslationProvider
{
    private readonly HttpClient _httpClient;
    private readonly TranslationSettings _settings;
    private readonly ILogger<AzureTranslationProvider> _logger;

    public AzureTranslationProvider(
        HttpClient httpClient,
        IOptions<TranslationSettings> settings,
        ILogger<AzureTranslationProvider> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        string.Equals(_settings.Provider, "Azure", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(_settings.ApiKey) &&
        Uri.TryCreate(_settings.Endpoint, UriKind.Absolute, out _);

    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            var endpoint = _settings.Endpoint.TrimEnd('/');
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{endpoint}/translate?api-version=3.0&from={Uri.EscapeDataString(sourceLanguage)}&to={Uri.EscapeDataString(targetLanguage)}");
            request.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ApiKey);
            if (!string.IsNullOrWhiteSpace(_settings.Region))
                request.Headers.Add("Ocp-Apim-Subscription-Region", _settings.Region);
            request.Content = JsonContent.Create(new[] { new { Text = text } });

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Translation provider {StatusCode} statusu qaytardı.",
                    response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
            return document.RootElement[0]
                .GetProperty("translations")[0]
                .GetProperty("text")
                .GetString();
        }
        catch (Exception exception) when (
            exception is HttpRequestException or
            TaskCanceledException or
            JsonException)
        {
            _logger.LogWarning(exception, "Dynamic translation provider request failed.");
            return null;
        }
    }
}
