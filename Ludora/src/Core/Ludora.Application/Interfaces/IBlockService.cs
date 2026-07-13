using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces;

public interface IBlockService
{
    Task BlockAsync(Guid blockerId, Guid blockedUserId);
    Task UnblockAsync(Guid blockerId, Guid blockedUserId);
    Task<List<BlockedUserDTO>> GetBlockedUsersAsync(Guid userId);
}