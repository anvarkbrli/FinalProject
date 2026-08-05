using System.Text.RegularExpressions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models.Steam;
using Ludora.Application.Settings;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Ludora.Infrastructure.Steam;

public sealed partial class SteamOpenIdClient : ISteamOpenIdClient
{
    private const string OpenIdNamespace = "http://specs.openid.net/auth/2.0";
    private const string IdentifierSelect = "http://specs.openid.net/auth/2.0/identifier_select";
    private readonly HttpClient _httpClient;
    private readonly SteamSettings _settings;
    private readonly SteamRequestThrottle _requestThrottle;

    public SteamOpenIdClient(
        HttpClient httpClient,
        IOptions<SteamSettings> options,
        SteamRequestThrottle requestThrottle)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _requestThrottle = requestThrottle;
    }

    public string BuildAuthenticationUrl(string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        var returnTo = QueryHelpers.AddQueryString(
            _settings.OpenIdCallbackUrl,
            "state",
            state);

        return QueryHelpers.AddQueryString(
            _settings.OpenIdEndpoint,
            new Dictionary<string, string?>
            {
                ["openid.ns"] = OpenIdNamespace,
                ["openid.mode"] = "checkid_setup",
                ["openid.return_to"] = returnTo,
                ["openid.realm"] = _settings.OpenIdRealm,
                ["openid.identity"] = IdentifierSelect,
                ["openid.claimed_id"] = IdentifierSelect
            });
    }

    public async Task<SteamOpenIdValidationModel> ValidateCallbackAsync(
        IReadOnlyDictionary<string, string> callbackParameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callbackParameters);

        if (!callbackParameters.TryGetValue("openid.mode", out var mode) ||
            !string.Equals(mode, "id_res", StringComparison.Ordinal))
        {
            return new SteamOpenIdValidationModel();
        }

        callbackParameters.TryGetValue("openid.claimed_id", out var claimedId);
        callbackParameters.TryGetValue("openid.identity", out var identity);
        callbackParameters.TryGetValue("openid.return_to", out var returnTo);

        callbackParameters.TryGetValue("state", out var callbackState);

        if (string.IsNullOrWhiteSpace(claimedId) ||
            !string.Equals(claimedId, identity, StringComparison.Ordinal) ||
            !IsExpectedReturnTo(returnTo, callbackState))
        {
            return new SteamOpenIdValidationModel();
        }

        var match = SteamClaimedIdRegex().Match(claimedId);
        if (!match.Success ||
            !ulong.TryParse(match.Groups["steamId"].Value, out _))
        {
            return new SteamOpenIdValidationModel();
        }

        var verificationParameters = callbackParameters
            .Where(x => x.Key.StartsWith("openid.", StringComparison.Ordinal))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        verificationParameters["openid.mode"] = "check_authentication";

        using var lease = await _requestThrottle.EnterAsync(cancellationToken);
        using var content = new FormUrlEncodedContent(verificationParameters);
        using var response = await _httpClient.PostAsync(
            _settings.OpenIdEndpoint,
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new SteamOpenIdValidationModel();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var isValid = body
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(line => string.Equals(line, "is_valid:true", StringComparison.OrdinalIgnoreCase));

        return new SteamOpenIdValidationModel
        {
            IsValid = isValid,
            SteamId = isValid ? match.Groups["steamId"].Value : null,
            ClaimedIdentifier = claimedId,
            ReturnTo = returnTo
        };
    }

    private bool IsExpectedReturnTo(string? returnTo, string? callbackState)
    {
        if (string.IsNullOrWhiteSpace(callbackState) ||
            !Uri.TryCreate(returnTo, UriKind.Absolute, out var returnedUri) ||
            !Uri.TryCreate(_settings.OpenIdCallbackUrl, UriKind.Absolute, out var callbackUri))
        {
            return false;
        }

        if (!string.Equals(
                returnedUri.GetLeftPart(UriPartial.Path).TrimEnd('/'),
                callbackUri.GetLeftPart(UriPartial.Path).TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var returnToQuery = QueryHelpers.ParseQuery(returnedUri.Query);
        return returnToQuery.TryGetValue("state", out var signedState) &&
               string.Equals(signedState.ToString(), callbackState, StringComparison.Ordinal);
    }

    [GeneratedRegex(
        "^https?://steamcommunity\\.com/openid/id/(?<steamId>[0-9]{17})/?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SteamClaimedIdRegex();
}
