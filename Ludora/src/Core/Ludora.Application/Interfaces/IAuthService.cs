using Ludora.Application.DTOs;
using Ludora.Application.DTOs.JWT;

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

    Task<RevokeAuthDeviceSessionResultDTO> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        string? currentRefreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
