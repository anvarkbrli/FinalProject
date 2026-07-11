using Ludora.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.Interfaces
{
    public interface IFollowService
    {
        Task FollowAsync(Guid followerId, Guid followingId);
        Task UnfollowAsync(Guid followerId, Guid followingId);
        Task<FollowStatusDTO> GetFollowStatusAsync(Guid currentUserId, Guid profileUserId);
        Task<List<UserListItemDTO>> GetFollowersAsync(Guid userId, Guid? currentUserId);
        Task<List<UserListItemDTO>> GetFollowingAsync(Guid userId, Guid? currentUserId);
    }
}
