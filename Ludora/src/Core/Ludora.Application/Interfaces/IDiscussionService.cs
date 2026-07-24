using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces;

public interface IDiscussionService
{
    Task<PagedResultDTO<DiscussionListItemDTO>> GetAllAsync(
        DiscussionFilterDTO filter,
        Guid? currentUserId,
        bool canModerate,
        CancellationToken cancellationToken = default);

    Task<PagedResultDTO<DiscussionListItemDTO>> GetDeletedAsync(
        DiscussionFilterDTO filter,
        CancellationToken cancellationToken = default);

    Task<DiscussionDetailDTO> GetByIdAsync(
        int id,
        Guid? currentUserId,
        bool canModerate,
        CancellationToken cancellationToken = default);

    Task<DiscussionDetailDTO> GetBySlugAsync(
        string slug,
        Guid? currentUserId,
        bool canModerate,
        CancellationToken cancellationToken = default);

    Task<DiscussionDetailDTO> CreateAsync(
        Guid userId,
        DiscussionCreateDTO dto,
        CancellationToken cancellationToken = default);

    Task<DiscussionDetailDTO> UpdateAsync(
        int id,
        Guid userId,
        bool canModerate,
        DiscussionUpdateDTO dto,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        int id,
        Guid userId,
        bool canModerate,
        CancellationToken cancellationToken = default);

    Task LikeAsync(int id, Guid userId, CancellationToken cancellationToken = default);
    Task UnlikeAsync(int id, Guid userId, CancellationToken cancellationToken = default);
    Task SaveAsync(int id, Guid userId, CancellationToken cancellationToken = default);
    Task UnsaveAsync(int id, Guid userId, CancellationToken cancellationToken = default);
    Task SetPinnedAsync(int id, bool isPinned, CancellationToken cancellationToken = default);
    Task SetLockedAsync(int id, bool isLocked, CancellationToken cancellationToken = default);
    Task<DiscussionDetailDTO> RestoreAsync(int id, CancellationToken cancellationToken = default);
}
