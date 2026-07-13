using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces;

public interface IFollowService
{
    Task<FollowStatusDTO> FollowAsync(Guid followerId, Guid followingId);
    Task UnfollowAsync(Guid followerId, Guid followingId);
    Task AcceptFollowRequestAsync(Guid currentUserId, Guid requesterId);
    Task RejectFollowRequestAsync(Guid currentUserId, Guid requesterId);
    Task RemoveFollowerAsync(Guid currentUserId, Guid followerId);
    Task<List<FollowRequestDTO>> GetFollowRequestsAsync(Guid userId);
    Task<FollowStatusDTO> GetFollowStatusAsync(Guid currentUserId, Guid profileUserId);
    Task<List<UserListItemDTO>> GetFollowersAsync(Guid userId, Guid? currentUserId);
    Task<List<UserListItemDTO>> GetFollowingAsync(Guid userId, Guid? currentUserId);
}