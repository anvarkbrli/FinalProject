using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces;

public interface IAccountDeletionService
{
    Task<DeleteAccountResultDTO> RequestDeletionAsync(
        Guid userId,
        DeleteAccountRequestDTO dto,
        CancellationToken cancellationToken = default);

    Task TryFinalizeAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task HardDeleteCommunityAsync(
        int communityId,
        CancellationToken cancellationToken = default);
}
