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

    public sealed record BackgroundJobLease(
        long JobId,
        Guid UserId,
        int AttemptCount);

    public interface IBadgeEvaluationQueue
    {
        Task<bool> EnqueueAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<BackgroundJobLease?> TryDequeueAsync(
            CancellationToken cancellationToken = default);

        Task CompleteAsync(
            long jobId,
            CancellationToken cancellationToken = default);

        Task FailAsync(
            long jobId,
            string error,
            CancellationToken cancellationToken = default);
    }

    public interface ISteamSyncQueue
    {
        Task<bool> EnqueueAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<BackgroundJobLease?> TryDequeueAsync(
            CancellationToken cancellationToken = default);

        Task CompleteAsync(
            long jobId,
            CancellationToken cancellationToken = default);

        Task FailAsync(
            long jobId,
            string error,
            CancellationToken cancellationToken = default);
    }
}
