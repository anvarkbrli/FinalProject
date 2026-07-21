using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models.Notification;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Ludora.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public class PostService : IPostService
{
    private const int MaxContentLength = 2000;

    private readonly AppDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly INotificationService _notificationService;

    public PostService(
        AppDbContext context,
        IFileStorageService fileStorageService,
        INotificationService notificationService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _notificationService = notificationService;
    }

    public async Task<PostDTO> CreateAsync(
        Guid userId,
        PostCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var userExists = await _context.Users
            .AnyAsync(x => x.Id == userId, cancellationToken);

        if (!userExists)
            throw new NotFoundException("İstifadəçi tapılmadı.");

        var content = dto.Content.NormalizeOptionalText(
            "Post mətni",
            MaxContentLength);

        if (content is null && dto.Image is null)
            throw new BadRequestException("Post üçün mətn və ya şəkil əlavə edilməlidir.");

        string? imageUrl = null;
        var postWasCommitted = false;

        try
        {
            if (dto.Image is not null)
                imageUrl = await _fileStorageService.SavePostImageAsync(dto.Image);

            var post = new UserPost
            {
                UserId = userId,
                Content = content,
                ImageUrl = imageUrl,
                CreatedAt = DateTime.UtcNow
            };

            await using var transaction = await _context.Database
                .BeginTransactionAsync(cancellationToken);

            await _context.UserPosts.AddAsync(post, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _context.AddUserActivity(
                userId,
                UserActivityType.PostCreated,
                postId: post.Id);

            await _notificationService.QueueForFollowersAsync(
                new FollowerNotificationRequest
                {
                    ActorUserId = userId,
                    Type = NotificationType.FollowingPostCreated,
                    EntityType = NotificationEntityType.Post,
                    EntityId = post.Id.ToString(),
                    Title = "Yeni post",
                    Message = "Yeni post paylaşdı.",
                    ActionUrl = "/"
                },
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            postWasCommitted = true;

            return await GetByIdAsync(post.Id, userId, cancellationToken);
        }
        catch
        {
            if (!postWasCommitted && imageUrl is not null)
                await _fileStorageService.DeleteAsync(imageUrl);

            throw;
        }
    }

    public async Task<PostDTO> GetByIdAsync(
        long postId,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var post = await _context.UserPosts
            .AsNoTracking()
            .Where(x => x.Id == postId)
            .Select(x => new
            {
                Post = x,
                x.User.IsPrivate
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Post tapılmadı.");

        await EnsureCanViewUserPostsAsync(
            post.Post.UserId,
            post.IsPrivate,
            currentUserId,
            cancellationToken);

        return await MapPostAsync(postId, currentUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<PostDTO>> GetUserPostsAsync(
        Guid userId,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.Id,
                x.IsPrivate
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("İstifadəçi tapılmadı.");

        await EnsureCanViewUserPostsAsync(
            user.Id,
            user.IsPrivate,
            currentUserId,
            cancellationToken);

        return await _context.UserPosts
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PostDTO
            {
                Id = x.Id,
                UserId = x.UserId,
                UserName = x.User.UserName ?? string.Empty,
                ProfileImageUrl = x.User.ProfileImageUrl,
                Content = x.Content,
                ImageUrl = x.ImageUrl,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                IsMine = currentUserId.HasValue && x.UserId == currentUserId.Value,
                LikeCount = x.Likes.Count,
                CommentCount = x.Comments.Count,
                IsLikedByCurrentUser = currentUserId.HasValue &&
                    x.Likes.Any(like => like.UserId == currentUserId.Value)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PostDTO>> GetFeedAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var blockedUserIds = _context.UserBlocks
            .Where(x =>
                x.BlockerId == currentUserId ||
                x.BlockedUserId == currentUserId)
            .Select(x =>
                x.BlockerId == currentUserId
                    ? x.BlockedUserId
                    : x.BlockerId);

        var followedUserIds = _context.UserFollows
            .Where(x =>
                x.FollowerId == currentUserId &&
                x.Status == FollowStatus.Accepted)
            .Select(x => x.FollowingId);

        return await _context.UserPosts
            .AsNoTracking()
            .Where(x =>
                (x.UserId == currentUserId || followedUserIds.Contains(x.UserId)) &&
                !blockedUserIds.Contains(x.UserId))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PostDTO
            {
                Id = x.Id,
                UserId = x.UserId,
                UserName = x.User.UserName ?? string.Empty,
                ProfileImageUrl = x.User.ProfileImageUrl,
                Content = x.Content,
                ImageUrl = x.ImageUrl,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                IsMine = x.UserId == currentUserId,
                LikeCount = x.Likes.Count,
                CommentCount = x.Comments.Count,
                IsLikedByCurrentUser = x.Likes.Any(like => like.UserId == currentUserId)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PostDTO> UpdateAsync(
        Guid userId,
        long postId,
        PostUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var post = await _context.UserPosts
            .FirstOrDefaultAsync(x => x.Id == postId, cancellationToken)
            ?? throw new NotFoundException("Post tapılmadı.");

        if (post.UserId != userId)
            throw new ForbiddenException("Bu postu dəyişmək icazəniz yoxdur.");

        if (dto.Image is not null && dto.RemoveImage)
            throw new BadRequestException("Yeni şəkil əlavə edilərkən mövcud şəkli ayrıca silmək seçilə bilməz.");

        var content = dto.Content.NormalizeOptionalText(
            "Post mətni",
            MaxContentLength);

        var resultingImageUrl = dto.RemoveImage
            ? null
            : post.ImageUrl;

        var oldImageUrl = post.ImageUrl;
        string? newImageUrl = null;
        var postWasCommitted = false;

        try
        {
            if (dto.Image is not null)
            {
                newImageUrl = await _fileStorageService.SavePostImageAsync(dto.Image);
                resultingImageUrl = newImageUrl;
            }

            if (content is null && resultingImageUrl is null)
                throw new BadRequestException("Post üçün mətn və ya şəkil saxlanmalıdır.");

            post.Content = content;
            post.ImageUrl = resultingImageUrl;
            post.UpdatedAt = DateTime.UtcNow;

            _context.AddUserActivity(
                userId,
                UserActivityType.PostUpdated,
                postId: post.Id);

            await _context.SaveChangesAsync(cancellationToken);
            postWasCommitted = true;
        }
        catch
        {
            if (!postWasCommitted && newImageUrl is not null)
                await _fileStorageService.DeleteAsync(newImageUrl);

            throw;
        }

        if (oldImageUrl is not null && oldImageUrl != post.ImageUrl)
            await _fileStorageService.DeleteAsync(oldImageUrl);

        return await GetByIdAsync(post.Id, userId, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid userId,
        long postId,
        CancellationToken cancellationToken = default)
    {
        var post = await _context.UserPosts
            .FirstOrDefaultAsync(x => x.Id == postId, cancellationToken)
            ?? throw new NotFoundException("Post tapılmadı.");

        if (post.UserId != userId)
            throw new ForbiddenException("Bu postu silmək icazəniz yoxdur.");

        post.IsDeleted = true;
        post.DeletedAtUtc = DateTime.UtcNow;
        post.DeletedByUserId = userId;
        post.DeleteReason = null;
        post.UpdatedAt = DateTime.UtcNow;

        _context.AddUserActivity(
            userId,
            UserActivityType.PostDeleted,
            postId: post.Id);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<PostDTO> MapPostAsync(
        long postId,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        return await _context.UserPosts
            .AsNoTracking()
            .Where(x => x.Id == postId)
            .Select(x => new PostDTO
            {
                Id = x.Id,
                UserId = x.UserId,
                UserName = x.User.UserName ?? string.Empty,
                ProfileImageUrl = x.User.ProfileImageUrl,
                Content = x.Content,
                ImageUrl = x.ImageUrl,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                IsMine = currentUserId.HasValue && x.UserId == currentUserId.Value,
                LikeCount = x.Likes.Count,
                CommentCount = x.Comments.Count,
                IsLikedByCurrentUser = currentUserId.HasValue &&
                    x.Likes.Any(like => like.UserId == currentUserId.Value)
            })
            .FirstAsync(cancellationToken);
    }

    private async Task EnsureCanViewUserPostsAsync(
        Guid profileUserId,
        bool isPrivate,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId.HasValue)
        {
            var isBlocked = await _context.UserBlocks
                .AnyAsync(x =>
                    (x.BlockerId == currentUserId.Value && x.BlockedUserId == profileUserId) ||
                    (x.BlockerId == profileUserId && x.BlockedUserId == currentUserId.Value),
                    cancellationToken);

            if (isBlocked)
                throw new ForbiddenException("Bu istifadəçinin postlarına baxmaq mümkün deyil.");
        }

        if (!isPrivate || currentUserId == profileUserId)
            return;

        if (!currentUserId.HasValue)
            throw new ForbiddenException("Bu hesab gizlidir.");

        var isFollowing = await _context.UserFollows
            .AnyAsync(x =>
                x.FollowerId == currentUserId.Value &&
                x.FollowingId == profileUserId &&
                x.Status == FollowStatus.Accepted,
                cancellationToken);

        if (!isFollowing)
            throw new ForbiddenException("Bu hesab gizlidir.");
    }
}
