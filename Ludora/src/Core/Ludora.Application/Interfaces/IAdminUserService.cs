using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Admin;

namespace Ludora.Application.Interfaces;

public interface IAdminUserService
{
    Task<PagedResultDTO<AdminUserListItemDTO>> GetAllAsync(
        AdminUserFilterDTO filter,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetailDTO> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AdminUserBanHistoryDTO> GetBanHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AdminUserWarningHistoryDTO> GetWarningHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserWarningDTO> WarnAsync(
        Guid adminUserId,
        Guid userId,
        CreateUserWarningDTO dto,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetailDTO> BanAsync(
        Guid adminUserId,
        Guid userId,
        BanUserDTO dto,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetailDTO> UnbanAsync(
        Guid adminUserId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetailDTO> ChangeRoleAsync(
        Guid adminUserId,
        Guid userId,
        ChangeUserRoleDTO dto,
        CancellationToken cancellationToken = default);
}
