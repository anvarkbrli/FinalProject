using Ludora.Application.Models.Steam;

namespace Ludora.Application.Interfaces;

public interface ISteamOpenIdClient
{
    string BuildAuthenticationUrl(string state);

    Task<SteamOpenIdValidationModel> ValidateCallbackAsync(
        IReadOnlyDictionary<string, string> callbackParameters,
        CancellationToken cancellationToken = default);
}
