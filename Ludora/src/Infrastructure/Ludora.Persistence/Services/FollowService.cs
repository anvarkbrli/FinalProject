using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services
{
    public class FollowService : IFollowService
    {
        private readonly AppDbContext _context;

        public FollowService(AppDbContext context)
        {
            _context = context;
        }

        public async Task FollowAsync(Guid followerId, Guid followingId)
        {
            if (followerId == followingId)
                throw new BadRequestException("İstifadəçi özünü izləyə bilməz.");

            var followingUserExists = await _context.Users.AnyAsync(u => u.Id == followingId);

            if (!followingUserExists)
                throw new NotFoundException("İstifadəçi tapılmadı.");

            var alreadyFollowing = await _context.UserFollows.AnyAsync(f =>
                f.FollowerId == followerId && f.FollowingId == followingId);

            if (alreadyFollowing)
                throw new ConflictException("Bu istifadəçini artıq izləyirsiniz.");

            var userFollow = new UserFollow
            {
                FollowerId = followerId,
                FollowingId = followingId,
                CreatedAt = DateTime.UtcNow
            };

            await _context.UserFollows.AddAsync(userFollow);
            await _context.SaveChangesAsync();
        }

        public async Task UnfollowAsync(Guid followerId, Guid followingId)
        {
            var userFollow = await _context.UserFollows.FirstOrDefaultAsync(f =>
                f.FollowerId == followerId && f.FollowingId == followingId);

            if (userFollow is null)
                throw new NotFoundException("İzləmə əlaqəsi tapılmadı.");

            _context.UserFollows.Remove(userFollow);
            await _context.SaveChangesAsync();
        }

        public async Task<FollowStatusDTO> GetFollowStatusAsync(Guid currentUserId, Guid profileUserId)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == profileUserId);

            if (!userExists)
                throw new NotFoundException("İstifadəçi tapılmadı.");

            var isFollowing = await _context.UserFollows.AnyAsync(f =>
                f.FollowerId == currentUserId && f.FollowingId == profileUserId);

            return new FollowStatusDTO
            {
                UserId = profileUserId,
                IsFollowing = isFollowing
            };
        }

        public async Task<List<UserListItemDTO>> GetFollowersAsync(Guid userId, Guid? currentUserId)
        {
            await CheckUserExistsAsync(userId);

            var currentUserFollowingIds = await GetCurrentUserFollowingIdsAsync(currentUserId);

            return await _context.UserFollows
                .AsNoTracking()
                .Where(f => f.FollowingId == userId)
                .Select(f => new UserListItemDTO
                {
                    Id = f.FollowerId,
                    UserName = f.Follower.UserName ?? string.Empty,
                    ProfileImageUrl = f.Follower.ProfileImageUrl,
                    IsFollowing = currentUserFollowingIds.Contains(f.FollowerId)
                })
                .ToListAsync();
        }

        public async Task<List<UserListItemDTO>> GetFollowingAsync(Guid userId, Guid? currentUserId)
        {
            await CheckUserExistsAsync(userId);

            var currentUserFollowingIds = await GetCurrentUserFollowingIdsAsync(currentUserId);

            return await _context.UserFollows
                .AsNoTracking()
                .Where(f => f.FollowerId == userId)
                .Select(f => new UserListItemDTO
                {
                    Id = f.FollowingId,
                    UserName = f.Following.UserName ?? string.Empty,
                    ProfileImageUrl = f.Following.ProfileImageUrl,
                    IsFollowing = currentUserFollowingIds.Contains(f.FollowingId)
                })
                .ToListAsync();
        }

        private async Task CheckUserExistsAsync(Guid userId)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);

            if (!userExists)
                throw new NotFoundException("İstifadəçi tapılmadı.");
        }

        private async Task<List<Guid>> GetCurrentUserFollowingIdsAsync(Guid? currentUserId)
        {
            if (!currentUserId.HasValue)
                return new List<Guid>();

            return await _context.UserFollows
                .Where(f => f.FollowerId == currentUserId.Value)
                .Select(f => f.FollowingId)
                .ToListAsync();
        }
    }
}