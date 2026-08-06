using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface IFollowService
    {
        Task<FollowStatusDTO> FollowAsync(
            Guid followerId,
            Guid followingId,
            CancellationToken cancellationToken = default);
        Task UnfollowAsync(
            Guid followerId,
            Guid followingId,
            CancellationToken cancellationToken = default);
        Task AcceptFollowRequestAsync(
            Guid currentUserId,
            Guid requesterId,
            CancellationToken cancellationToken = default);
        Task RejectFollowRequestAsync(
            Guid currentUserId,
            Guid requesterId,
            CancellationToken cancellationToken = default);
        Task RemoveFollowerAsync(
            Guid currentUserId,
            Guid followerId,
            CancellationToken cancellationToken = default);
        Task<PagedResultDTO<FollowRequestDTO>> GetFollowRequestsAsync(
            Guid userId,
            PaginationFilterDTO pagination,
            string? search = null,
            CancellationToken cancellationToken = default);
        Task<int> AcceptFollowRequestsAsync(
            Guid currentUserId,
            IReadOnlyCollection<Guid> requesterIds,
            CancellationToken cancellationToken = default);
        Task<int> RejectFollowRequestsAsync(
            Guid currentUserId,
            IReadOnlyCollection<Guid> requesterIds,
            CancellationToken cancellationToken = default);
        Task<FollowStatusDTO> GetFollowStatusAsync(
            Guid currentUserId,
            Guid profileUserId,
            CancellationToken cancellationToken = default);
        Task<PagedResultDTO<UserListItemDTO>> GetFollowersAsync(
            Guid userId,
            Guid? currentUserId,
            PaginationFilterDTO pagination,
            CancellationToken cancellationToken = default);
        Task<PagedResultDTO<UserListItemDTO>> GetFollowingAsync(
            Guid userId,
            Guid? currentUserId,
            PaginationFilterDTO pagination,
            CancellationToken cancellationToken = default);
    }
}
