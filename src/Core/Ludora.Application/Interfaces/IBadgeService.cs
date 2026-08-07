using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Badge;

namespace Ludora.Application.Interfaces
{
    public interface IBadgeService
    {
        Task<IReadOnlyList<BadgeDTO>> GetAllAsync(
            bool includeInactive,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserBadgeDTO>> GetUserBadgesAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
        Task<BadgeDTO> CreateAsync(
            BadgeUpsertDTO dto,
            CancellationToken cancellationToken = default);
        Task<BadgeDTO> UpdateAsync(
            int id,
            BadgeUpsertDTO dto,
            CancellationToken cancellationToken = default);
        Task DeleteAsync(
            int id,
            CancellationToken cancellationToken = default);
        Task<BadgeDTO> RestoreAsync(
            int id,
            CancellationToken cancellationToken = default);
        Task<BadgeDTO> UpdateIconAsync(
            int id,
            ProfileImageUploadDTO icon,
            CancellationToken cancellationToken = default);
        Task<int> EvaluateUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
        Task<UserBadgeDTO> AssignToUserAsync(
            Guid userId,
            int badgeDefinitionId,
            CancellationToken cancellationToken = default);
        Task RevokeFromUserAsync(
            Guid userId,
            int badgeDefinitionId,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Guid>> GetUserBatchAsync(
            int skip,
            int take,
            CancellationToken cancellationToken = default);
    }
}
