using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface IUserWarningService
    {
        Task<PagedResultDTO<UserWarningDTO>> GetMineAsync(
            Guid userId,
            bool? isRead,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<UserWarningDTO> MarkAsReadAsync(
            Guid userId,
            int warningId,
            CancellationToken cancellationToken = default);

        Task MarkAllAsReadAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
    }
}
