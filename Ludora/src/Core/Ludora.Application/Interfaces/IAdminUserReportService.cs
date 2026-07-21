using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces;

public interface IAdminUserReportService
{
    Task<PagedResultDTO<AdminReportedUserListItemDTO>> GetReportedUsersAsync(
        AdminUserReportFilterDTO filter,
        CancellationToken cancellationToken = default);

    Task<AdminReportedUserDetailDTO> GetReportedUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AdminUserReportItemDTO> UpdateStatusAsync(
        Guid adminUserId,
        long reportId,
        AdminUserReportStatusUpdateDTO dto,
        CancellationToken cancellationToken = default);

    Task<StoredFileDTO> GetEvidenceAsync(
        long reportId,
        CancellationToken cancellationToken = default);
}
