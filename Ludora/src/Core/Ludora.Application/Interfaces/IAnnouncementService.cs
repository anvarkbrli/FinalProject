using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Admin;

namespace Ludora.Application.Interfaces;

public interface IAnnouncementService
{
    Task<PagedResultDTO<AnnouncementDTO>> GetPublishedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AnnouncementDTO> GetPublishedByIdAsync(
        int announcementId,
        CancellationToken cancellationToken = default);

    Task<PagedResultDTO<AnnouncementDTO>> GetAllForAdminAsync(
        AdminAnnouncementFilterDTO filter,
        CancellationToken cancellationToken = default);

    Task<AnnouncementDTO> GetByIdForAdminAsync(
        int announcementId,
        CancellationToken cancellationToken = default);

    Task<AnnouncementDTO> CreateAsync(
        Guid adminUserId,
        AnnouncementCreateDTO dto,
        CancellationToken cancellationToken = default);

    Task<AnnouncementDTO> UpdateAsync(
        int announcementId,
        AnnouncementUpdateDTO dto,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        int announcementId,
        CancellationToken cancellationToken = default);

    Task<AnnouncementDTO> RestoreAsync(
        int announcementId,
        CancellationToken cancellationToken = default);
}
