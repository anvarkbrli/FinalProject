using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface IBlockService
    {
        Task BlockAsync(
            Guid blockerId,
            Guid blockedUserId,
            CancellationToken cancellationToken = default);
        Task UnblockAsync(
            Guid blockerId,
            Guid blockedUserId,
            CancellationToken cancellationToken = default);
        Task<PagedResultDTO<BlockedUserDTO>> GetBlockedUsersAsync(
            Guid userId,
            PaginationFilterDTO pagination,
            CancellationToken cancellationToken = default);
    }
}
