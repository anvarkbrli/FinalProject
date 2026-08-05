using System.ComponentModel.DataAnnotations;
using Ludora.Application.DTOs.JWT;

namespace Ludora.Application.DTOs.ExternalAuth;

public sealed class ExternalProviderProfileDTO
{
    public string Provider { get; init; } = string.Empty;
    public string ProviderKey { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool EmailVerified { get; init; }
    public string? DisplayName { get; init; }
    public string? PictureUrl { get; init; }
}

public sealed class ExternalAuthCallbackResultDTO
{
    public string Code { get; init; } = string.Empty;
    public string ReturnPath { get; init; } = "/";
    public bool RequiresProfileCompletion { get; init; }
}

public sealed class ExternalAuthExchangeRequestDTO
{
    [Required, StringLength(256, MinimumLength = 20)]
    public string Code { get; init; } = string.Empty;
}

public sealed class ExternalAuthExchangeResultDTO
{
    public bool RequiresProfileCompletion { get; init; }
    public AuthResponseDTO? Session { get; init; }
    public string? RegistrationToken { get; init; }
    public string? SuggestedUserName { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
    public string? PictureUrl { get; init; }
    public string ReturnPath { get; init; } = "/";
}

public sealed class ExternalAuthExchangeServiceResultDTO
{
    public bool RequiresProfileCompletion { get; init; }
    public AuthSessionResultDTO? Session { get; init; }
    public string? RegistrationToken { get; init; }
    public string? SuggestedUserName { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
    public string? PictureUrl { get; init; }
    public string ReturnPath { get; init; } = "/";
}

public sealed class CompleteExternalRegistrationDTO
{
    [Required, StringLength(256, MinimumLength = 20)]
    public string RegistrationToken { get; init; } = string.Empty;

    [Required, StringLength(30, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9_.]+$")]
    public string UserName { get; init; } = string.Empty;

    public bool AcceptedTermsAndPrivacy { get; init; }
    public bool UseGoogleProfileImage { get; init; } = true;
}

public sealed class ExternalLoginStatusDTO
{
    public bool GoogleEnabled { get; init; }
    public bool GoogleLinked { get; init; }
    public bool HasPassword { get; init; }
    public bool CanUnlinkGoogle { get; init; }
    public string? GoogleEmail { get; init; }
}

public sealed class ExternalLoginOperationResultDTO
{
    public string Message { get; init; } = string.Empty;
}

public sealed class ExternalRegistrationServiceResultDTO
{
    public AuthSessionResultDTO Session { get; init; } = new();
    public string ReturnPath { get; init; } = "/";
}
