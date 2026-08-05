using Ludora.Application.DTOs;
using Ludora.Application.DTOs.JWT;
using Ludora.Application.DTOs.ExternalAuth;

namespace Ludora.Application.Interfaces;

public interface IAuthService
{
    Task<UsernameAvailabilityDTO> CheckUsernameAvailabilityAsync(
        string userName,
        CancellationToken cancellationToken = default);

    Task<RegisterResultDTO> RegisterAsync(
        RegisterDTO dto,
        CancellationToken cancellationToken = default);

    Task<AuthSessionResultDTO> LoginAsync(
        LoginDTO dto,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<AuthSessionResultDTO> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(
        string? refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<EmailOperationResultDTO> ConfirmEmailAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default);

    Task<EmailOperationResultDTO> ResendEmailConfirmationAsync(
        ResendEmailConfirmationDTO dto,
        CancellationToken cancellationToken = default);

    Task<EmailOperationResultDTO> ForgotPasswordAsync(
        ForgotPasswordDTO dto,
        CancellationToken cancellationToken = default);

    Task<EmailOperationResultDTO> ResetPasswordAsync(
        ResetPasswordDTO dto,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuthDeviceSessionDTO>> GetActiveSessionsAsync(
        Guid userId,
        string? currentRefreshToken,
        CancellationToken cancellationToken = default);

    Task<ExternalAuthCallbackResultDTO> PrepareExternalLoginAsync(
        ExternalProviderProfileDTO profile,
        string returnPath,
        CancellationToken cancellationToken = default);

    Task<ExternalAuthExchangeServiceResultDTO> ExchangeExternalAuthCodeAsync(
        string code,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<ExternalRegistrationServiceResultDTO> CompleteExternalRegistrationAsync(
        CompleteExternalRegistrationDTO dto,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<string> CreateGoogleLinkIntentAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Guid> ConsumeGoogleLinkIntentAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<ExternalLoginStatusDTO> GetExternalLoginStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ExternalLoginOperationResultDTO> LinkExternalLoginAsync(
        Guid userId,
        ExternalProviderProfileDTO profile,
        CancellationToken cancellationToken = default);

    Task<ExternalLoginOperationResultDTO> UnlinkGoogleAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<RevokeAuthDeviceSessionResultDTO> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        string? currentRefreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
