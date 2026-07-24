using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models.Notification;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Ludora.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public class FollowService : IFollowService
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;

    public FollowService(
        AppDbContext context,
        INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<FollowStatusDTO> FollowAsync(
        Guid followerId,
        Guid followingId,
        CancellationToken cancellationToken = default)
    {
        if (followerId == followingId)
            throw new BadRequestException("İstifadəçi özünü izləyə bilməz.");

        var followingUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.Id == followingId,
                cancellationToken)
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        var hasBlock = await _context.UserBlocks.AnyAsync(
            block =>
                block.BlockerId == followerId &&
                block.BlockedUserId == followingId ||
                block.BlockerId == followingId &&
                block.BlockedUserId == followerId,
            cancellationToken);

        if (hasBlock)
        {
            throw new ForbiddenException(
                "Bloklanmış istifadəçilər arasında izləmə əməliyyatı mümkün deyil.");
        }

        var existingFollow = await _context.UserFollows.FirstOrDefaultAsync(
            follow =>
                follow.FollowerId == followerId &&
                follow.FollowingId == followingId,
            cancellationToken);

        if (existingFollow is not null)
        {
            if (existingFollow.Status == FollowStatus.Accepted)
                throw new ConflictException("Bu istifadəçini artıq izləyirsiniz.");

            throw new ConflictException(
                "Bu istifadəçiyə artıq izləmə sorğusu göndərmisiniz.");
        }

        var status = followingUser.IsPrivate
            ? FollowStatus.Pending
            : FollowStatus.Accepted;
        var now = DateTime.UtcNow;

        var userFollow = new UserFollow
        {
            FollowerId = followerId,
            FollowingId = followingId,
            Status = status,
            CreatedAt = now,
            AcceptedAt = status == FollowStatus.Accepted ? now : null
        };

        await _context.UserFollows.AddAsync(
            userFollow,
            cancellationToken);

        _context.AddUserActivity(
            followerId,
            status == FollowStatus.Accepted
                ? UserActivityType.UserFollowed
                : UserActivityType.UserFollowRequested,
            targetUserId: followingId);

        await _notificationService.QueueAsync(
            new NotificationCreateRequest
            {
                RecipientUserId = followingId,
                ActorUserId = followerId,
                Type = status == FollowStatus.Accepted
                    ? NotificationType.UserFollowed
                    : NotificationType.FollowRequestReceived,
                Priority = status == FollowStatus.Pending
                    ? NotificationPriority.High
                    : NotificationPriority.Normal,
                EntityType = NotificationEntityType.User,
                EntityId = followerId.ToString(),
                Title = status == FollowStatus.Accepted
                    ? "Yeni izləyici"
                    : "Yeni izləmə sorğusu",
                Message = status == FollowStatus.Accepted
                    ? "Sizi izləməyə başladı."
                    : "Sizə izləmə sorğusu göndərdi."
            },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new FollowStatusDTO
        {
            UserId = followingId,
            IsFollowing = status == FollowStatus.Accepted,
            IsPending = status == FollowStatus.Pending
        };
    }

    public async Task UnfollowAsync(
        Guid followerId,
        Guid followingId,
        CancellationToken cancellationToken = default)
    {
        var userFollow = await _context.UserFollows.FirstOrDefaultAsync(
            follow =>
                follow.FollowerId == followerId &&
                follow.FollowingId == followingId,
            cancellationToken)
            ?? throw new NotFoundException(
                "İzləmə və ya izləmə sorğusu tapılmadı.");

        _context.UserFollows.Remove(userFollow);

        _context.AddUserActivity(
            followerId,
            userFollow.Status == FollowStatus.Pending
                ? UserActivityType.FollowRequestCancelled
                : UserActivityType.UserUnfollowed,
            targetUserId: followingId);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AcceptFollowRequestAsync(
        Guid currentUserId,
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        var followRequest = await _context.UserFollows.FirstOrDefaultAsync(
            follow =>
                follow.FollowerId == requesterId &&
                follow.FollowingId == currentUserId &&
                follow.Status == FollowStatus.Pending,
            cancellationToken)
            ?? throw new NotFoundException("İzləmə sorğusu tapılmadı.");

        followRequest.Status = FollowStatus.Accepted;
        followRequest.AcceptedAt = DateTime.UtcNow;

        _context.AddUserActivity(
            requesterId,
            UserActivityType.UserFollowed,
            targetUserId: currentUserId);

        await _notificationService.QueueAsync(
            new NotificationCreateRequest
            {
                RecipientUserId = requesterId,
                ActorUserId = currentUserId,
                Type = NotificationType.FollowRequestAccepted,
                Priority = NotificationPriority.High,
                EntityType = NotificationEntityType.User,
                EntityId = currentUserId.ToString(),
                Title = "İzləmə sorğusu qəbul edildi",
                Message = "Göndərdiyiniz izləmə sorğusunu qəbul etdi."
            },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectFollowRequestAsync(
        Guid currentUserId,
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        var followRequest = await _context.UserFollows.FirstOrDefaultAsync(
            follow =>
                follow.FollowerId == requesterId &&
                follow.FollowingId == currentUserId &&
                follow.Status == FollowStatus.Pending,
            cancellationToken)
            ?? throw new NotFoundException("İzləmə sorğusu tapılmadı.");

        _context.UserFollows.Remove(followRequest);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveFollowerAsync(
        Guid currentUserId,
        Guid followerId,
        CancellationToken cancellationToken = default)
    {
        var follow = await _context.UserFollows.FirstOrDefaultAsync(
            relation =>
                relation.FollowerId == followerId &&
                relation.FollowingId == currentUserId &&
                relation.Status == FollowStatus.Accepted,
            cancellationToken)
            ?? throw new NotFoundException("Bu istifadəçi sizi izləmir.");

        _context.UserFollows.Remove(follow);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResultDTO<FollowRequestDTO>> GetFollowRequestsAsync(
        Guid userId,
        PaginationFilterDTO pagination,
        CancellationToken cancellationToken = default)
    {
        var query = _context.UserFollows
            .AsNoTracking()
            .Where(follow =>
                follow.FollowingId == userId &&
                follow.Status == FollowStatus.Pending)
            .OrderByDescending(follow => follow.CreatedAt)
            .ThenByDescending(follow => follow.FollowerId)
            .Select(follow => new FollowRequestDTO
            {
                UserId = follow.FollowerId,
                UserName = follow.Follower.UserName ?? string.Empty,
                ProfileImageUrl = follow.Follower.ProfileImageUrl,
                RequestedAt = follow.CreatedAt
            });

        return await query.ToPagedResultAsync(
            pagination,
            cancellationToken);
    }

    public async Task<FollowStatusDTO> GetFollowStatusAsync(
        Guid currentUserId,
        Guid profileUserId,
        CancellationToken cancellationToken = default)
    {
        var userExists = await _context.Users
            .AnyAsync(
                user => user.Id == profileUserId,
                cancellationToken);

        if (!userExists)
            throw new NotFoundException("İstifadəçi tapılmadı.");

        var follow = await _context.UserFollows
            .AsNoTracking()
            .FirstOrDefaultAsync(
                relation =>
                    relation.FollowerId == currentUserId &&
                    relation.FollowingId == profileUserId,
                cancellationToken);

        return new FollowStatusDTO
        {
            UserId = profileUserId,
            IsFollowing = follow?.Status == FollowStatus.Accepted,
            IsPending = follow?.Status == FollowStatus.Pending
        };
    }

    public async Task<PagedResultDTO<UserListItemDTO>> GetFollowersAsync(
        Guid userId,
        Guid? currentUserId,
        PaginationFilterDTO pagination,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanViewConnectionsAsync(
            userId,
            currentUserId,
            cancellationToken);

        var query = _context.UserFollows
            .AsNoTracking()
            .Where(follow =>
                follow.FollowingId == userId &&
                follow.Status == FollowStatus.Accepted)
            .OrderByDescending(follow => follow.CreatedAt)
            .ThenByDescending(follow => follow.FollowerId)
            .Select(follow => new UserListItemDTO
            {
                Id = follow.FollowerId,
                UserName = follow.Follower.UserName ?? string.Empty,
                ProfileImageUrl = follow.Follower.ProfileImageUrl,
                IsFollowing = currentUserId.HasValue &&
                    _context.UserFollows.Any(current =>
                        current.FollowerId == currentUserId.Value &&
                        current.FollowingId == follow.FollowerId &&
                        current.Status == FollowStatus.Accepted)
            });

        return await query.ToPagedResultAsync(
            pagination,
            cancellationToken);
    }

    public async Task<PagedResultDTO<UserListItemDTO>> GetFollowingAsync(
        Guid userId,
        Guid? currentUserId,
        PaginationFilterDTO pagination,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanViewConnectionsAsync(
            userId,
            currentUserId,
            cancellationToken);

        var query = _context.UserFollows
            .AsNoTracking()
            .Where(follow =>
                follow.FollowerId == userId &&
                follow.Status == FollowStatus.Accepted)
            .OrderByDescending(follow => follow.CreatedAt)
            .ThenByDescending(follow => follow.FollowingId)
            .Select(follow => new UserListItemDTO
            {
                Id = follow.FollowingId,
                UserName = follow.Following.UserName ?? string.Empty,
                ProfileImageUrl = follow.Following.ProfileImageUrl,
                IsFollowing = currentUserId.HasValue &&
                    _context.UserFollows.Any(current =>
                        current.FollowerId == currentUserId.Value &&
                        current.FollowingId == follow.FollowingId &&
                        current.Status == FollowStatus.Accepted)
            });

        return await query.ToPagedResultAsync(
            pagination,
            cancellationToken);
    }

    private async Task EnsureCanViewConnectionsAsync(
        Guid userId,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.IsPrivate
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        if (currentUserId == userId)
            return;

        if (currentUserId.HasValue)
        {
            var blocked = await _context.UserBlocks
                .AsNoTracking()
                .AnyAsync(
                    block =>
                        block.BlockerId == currentUserId.Value &&
                        block.BlockedUserId == userId ||
                        block.BlockerId == userId &&
                        block.BlockedUserId == currentUserId.Value,
                    cancellationToken);

            if (blocked)
            {
                throw new ForbiddenException(
                    "Bu istifadəçinin əlaqələrinə baxmaq mümkün deyil.");
            }
        }

        if (!user.IsPrivate)
            return;

        var follows = currentUserId.HasValue &&
            await _context.UserFollows
                .AsNoTracking()
                .AnyAsync(
                    follow =>
                        follow.FollowerId == currentUserId.Value &&
                        follow.FollowingId == userId &&
                        follow.Status == FollowStatus.Accepted,
                    cancellationToken);

        if (!follows)
            throw new ForbiddenException("Bu profil gizlidir.");
    }
}
