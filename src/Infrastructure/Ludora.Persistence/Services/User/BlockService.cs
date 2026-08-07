using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Ludora.Application.Extensions;

using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services
{
    public class BlockService : IBlockService
    {
        private readonly AppDbContext _context;

        public BlockService(AppDbContext context)
        {
            _context = context;
        }

        public async Task BlockAsync(
            Guid blockerId,
            Guid blockedUserId,
            CancellationToken cancellationToken = default)
        {
            if (blockerId == blockedUserId)
                throw new BadRequestException("İstifadəçi özünü bloklaya bilməz.");

            var blockedUserExists = await _context.Users
                .AnyAsync(u => u.Id == blockedUserId, cancellationToken);

            if (!blockedUserExists)
                throw new NotFoundException("İstifadəçi tapılmadı.");

            var alreadyBlocked = await _context.UserBlocks.AnyAsync(
                ub => ub.BlockerId == blockerId && ub.BlockedUserId == blockedUserId,
                cancellationToken);

            if (alreadyBlocked)
                throw new ConflictException("Bu istifadəçini artıq bloklamısınız.");

            var followRelations = await _context.UserFollows
                .Where(uf =>
                    uf.FollowerId == blockerId && uf.FollowingId == blockedUserId ||
                    uf.FollowerId == blockedUserId && uf.FollowingId == blockerId)
                .ToListAsync(cancellationToken);

            if (followRelations.Count > 0)
                _context.UserFollows.RemoveRange(followRelations);

            var userBlock = new UserBlock
            {
                BlockerId = blockerId,
                BlockedUserId = blockedUserId,
                CreatedAt = DateTime.UtcNow
            };

            await _context.UserBlocks.AddAsync(userBlock, cancellationToken);

            _context.UserActivities.AddUserActivity(
                blockerId,
                UserActivityType.UserBlocked,
                targetUserId: blockedUserId);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UnblockAsync(
            Guid blockerId,
            Guid blockedUserId,
            CancellationToken cancellationToken = default)
        {
            var userBlock = await _context.UserBlocks.FirstOrDefaultAsync(
                ub => ub.BlockerId == blockerId && ub.BlockedUserId == blockedUserId,
                cancellationToken);

            if (userBlock is null)
                throw new NotFoundException("Bloklama əlaqəsi tapılmadı.");

            _context.UserBlocks.Remove(userBlock);

            _context.UserActivities.AddUserActivity(
                blockerId,
                UserActivityType.UserUnblocked,
                targetUserId: blockedUserId);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<PagedResultDTO<BlockedUserDTO>> GetBlockedUsersAsync(
            Guid userId,
            PaginationFilterDTO pagination,
            CancellationToken cancellationToken = default)
        {
            var query = _context.UserBlocks
                .AsNoTracking()
                .Where(ub => ub.BlockerId == userId)
                .OrderByDescending(ub => ub.CreatedAt)
                .ThenByDescending(ub => ub.BlockedUserId)
                .Select(ub => new BlockedUserDTO
                {
                    Id = ub.BlockedUserId,
                    UserName = ub.BlockedUser.UserName ?? string.Empty,
                    ProfileImageUrl = ub.BlockedUser.ProfileImageUrl,
                    BlockedAt = ub.CreatedAt
                });

            return await query.ToPagedResultAsync(
                pagination,
                cancellationToken);
        }
    }
}
