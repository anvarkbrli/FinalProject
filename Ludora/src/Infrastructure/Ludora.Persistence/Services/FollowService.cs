using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public class FollowService : IFollowService
{
    private readonly AppDbContext _context;

    public FollowService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<FollowStatusDTO> FollowAsync(Guid followerId, Guid followingId)
    {
        if (followerId == followingId)
            throw new BadRequestException("İstifadəçi özünü izləyə bilməz.");

        var followingUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == followingId);

        if (followingUser is null)
            throw new NotFoundException("İstifadəçi tapılmadı.");

        var hasBlock = await _context.UserBlocks.AnyAsync(ub =>
            ub.BlockerId == followerId && ub.BlockedUserId == followingId ||
            ub.BlockerId == followingId && ub.BlockedUserId == followerId);

        if (hasBlock) throw new ForbiddenException("Bloklanmış istifadəçilər arasında izləmə əməliyyatı mümkün deyil.");

        var existingFollow = await _context.UserFollows.FirstOrDefaultAsync(uf =>
            uf.FollowerId == followerId && uf.FollowingId == followingId);

        if (existingFollow is not null)
        {
            if (existingFollow.Status == FollowStatus.Accepted) throw new ConflictException("Bu istifadəçini artıq izləyirsiniz.");

            throw new ConflictException("Bu istifadəçiyə artıq izləmə sorğusu göndərmisiniz.");
        }

        var status = followingUser.IsPrivate ? FollowStatus.Pending : FollowStatus.Accepted;

        var userFollow = new UserFollow
        {
            FollowerId = followerId,
            FollowingId = followingId,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            AcceptedAt = status == FollowStatus.Accepted ? DateTime.UtcNow : null
        };

        await _context.UserFollows.AddAsync(userFollow);
        await _context.SaveChangesAsync();

        return new FollowStatusDTO
        {
            UserId = followingId,
            IsFollowing = status == FollowStatus.Accepted,
            IsPending = status == FollowStatus.Pending
        };
    }

    public async Task UnfollowAsync(Guid followerId, Guid followingId)
    {
        var userFollow = await _context.UserFollows.FirstOrDefaultAsync(uf =>
            uf.FollowerId == followerId && uf.FollowingId == followingId);

        if (userFollow is null) throw new NotFoundException("İzləmə və ya izləmə sorğusu tapılmadı.");

        _context.UserFollows.Remove(userFollow);
        await _context.SaveChangesAsync();
    }

    public async Task AcceptFollowRequestAsync(Guid currentUserId, Guid requesterId)
    {
        var followRequest = await _context.UserFollows.FirstOrDefaultAsync(uf =>
            uf.FollowerId == requesterId &&
            uf.FollowingId == currentUserId &&
            uf.Status == FollowStatus.Pending);

        if (followRequest is null)
            throw new NotFoundException("İzləmə sorğusu tapılmadı.");

        followRequest.Status = FollowStatus.Accepted;
        followRequest.AcceptedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task RejectFollowRequestAsync(Guid currentUserId, Guid requesterId)
    {
        var followRequest = await _context.UserFollows.FirstOrDefaultAsync(uf =>
            uf.FollowerId == requesterId &&
            uf.FollowingId == currentUserId &&
            uf.Status == FollowStatus.Pending);

        if (followRequest is null)
            throw new NotFoundException("İzləmə sorğusu tapılmadı.");

        _context.UserFollows.Remove(followRequest);
        await _context.SaveChangesAsync();
    }
    public async Task RemoveFollowerAsync(Guid currentUserId, Guid followerId)
    {
        var follow = await _context.UserFollows.FirstOrDefaultAsync(uf =>
            uf.FollowerId == followerId &&
            uf.FollowingId == currentUserId &&
            uf.Status == FollowStatus.Accepted);

        if (follow is null)
            throw new NotFoundException("Bu istifadəçi sizi izləmir.");

        _context.UserFollows.Remove(follow);
        await _context.SaveChangesAsync();
    }


    public async Task<List<FollowRequestDTO>> GetFollowRequestsAsync(Guid userId)
    {
        return await _context.UserFollows
            .AsNoTracking()
            .Where(uf => uf.FollowingId == userId && uf.Status == FollowStatus.Pending)
            .OrderByDescending(uf => uf.CreatedAt)
            .Select(uf => new FollowRequestDTO
            {
                UserId = uf.FollowerId,
                UserName = uf.Follower.UserName ?? string.Empty,
                ProfileImageUrl = uf.Follower.ProfileImageUrl,
                RequestedAt = uf.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<FollowStatusDTO> GetFollowStatusAsync(Guid currentUserId, Guid profileUserId)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == profileUserId);

        if (!userExists)
            throw new NotFoundException("İstifadəçi tapılmadı.");

        var follow = await _context.UserFollows
            .AsNoTracking()
            .FirstOrDefaultAsync(uf =>
                uf.FollowerId == currentUserId &&
                uf.FollowingId == profileUserId);

        return new FollowStatusDTO
        {
            UserId = profileUserId,
            IsFollowing = follow?.Status == FollowStatus.Accepted,
            IsPending = follow?.Status == FollowStatus.Pending
        };
    }

    public async Task<List<UserListItemDTO>> GetFollowersAsync(Guid userId, Guid? currentUserId)
    {
        await CheckUserExistsAsync(userId);

        var currentUserFollowingIds = await GetCurrentUserFollowingIdsAsync(currentUserId);

        return await _context.UserFollows
            .AsNoTracking()
            .Where(uf => uf.FollowingId == userId && uf.Status == FollowStatus.Accepted)
            .Select(uf => new UserListItemDTO
            {
                Id = uf.FollowerId,
                UserName = uf.Follower.UserName ?? string.Empty,
                ProfileImageUrl = uf.Follower.ProfileImageUrl,
                IsFollowing = currentUserFollowingIds.Contains(uf.FollowerId)
            })
            .ToListAsync();
    }

    public async Task<List<UserListItemDTO>> GetFollowingAsync(Guid userId, Guid? currentUserId)
    {
        await CheckUserExistsAsync(userId);

        var currentUserFollowingIds = await GetCurrentUserFollowingIdsAsync(currentUserId);

        return await _context.UserFollows
            .AsNoTracking()
            .Where(uf => uf.FollowerId == userId && uf.Status == FollowStatus.Accepted)
            .Select(uf => new UserListItemDTO
            {
                Id = uf.FollowingId,
                UserName = uf.Following.UserName ?? string.Empty,
                ProfileImageUrl = uf.Following.ProfileImageUrl,
                IsFollowing = currentUserFollowingIds.Contains(uf.FollowingId)
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
            .Where(uf => uf.FollowerId == currentUserId.Value && uf.Status == FollowStatus.Accepted)
            .Select(uf => uf.FollowingId)
            .ToListAsync();
    }
}