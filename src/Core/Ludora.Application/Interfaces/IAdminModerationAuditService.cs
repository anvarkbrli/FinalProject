using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface IAdminModerationAuditService
    {
        Task<PagedResultDTO<ModeratorActionLogDTO>> GetAllAsync(
            AdminModerationAuditFilterDTO filter,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ModeratorActionLogDTO>> GetByReportAsync(
            long reportId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ModeratorActionLogDTO>> GetByUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
    }
}
