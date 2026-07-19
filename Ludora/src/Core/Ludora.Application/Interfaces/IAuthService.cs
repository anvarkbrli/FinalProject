using Ludora.Application.DTOs;
using Ludora.Application.DTOs.JWT;

namespace Ludora.Application.Interfaces;

public interface IAuthService
{
    Task<RegisterResultDTO> RegisterAsync(
        RegisterDTO dto,
        CancellationToken cancellationToken = default);

    Task<AuthResponseDTO> LoginAsync(
        LoginDTO dto,
        CancellationToken cancellationToken = default);

    Task<EmailOperationResultDTO> ConfirmEmailAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default);

    Task<EmailOperationResultDTO> ResendEmailConfirmationAsync(
        ResendEmailConfirmationDTO dto,
        CancellationToken cancellationToken = default);
}
