using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface IAdminPostService
    {
        Task<PagedResultDTO<AdminPostDTO>> GetAllAsync(
            AdminPostFilterDTO filter,
            CancellationToken cancellationToken = default);

        Task<AdminPostDTO> GetByIdAsync(
            long postId,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            Guid adminUserId,
            long postId,
            ModeratePostDTO dto,
            CancellationToken cancellationToken = default);

        Task<AdminPostDTO> RestoreAsync(
            long postId,
            CancellationToken cancellationToken = default);
    }
}
