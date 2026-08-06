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

namespace Ludora.Persistence.Services
{
    public sealed class PostInteractionService : IPostInteractionService
    {
        private const int MaxCommentLength = 2000;
        private const int MaxPageSize = 100;
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;

        public PostInteractionService(
            AppDbContext context,
            INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task LikeAsync(long postId, Guid userId, CancellationToken cancellationToken = default)
        {
            var post = await GetAccessiblePostAsync(postId, userId, cancellationToken);

            if (post.UserId == userId)
                throw new BadRequestException("Öz postunuzu bəyənə bilməzsiniz.");

            var exists = await _context.PostLikes
                .AnyAsync(x => x.PostId == postId && x.UserId == userId, cancellationToken);

            if (exists)
                return;

            await _context.PostLikes.AddAsync(new PostLike
            {
                PostId = postId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            _context.AddUserActivity(
                userId,
                UserActivityType.PostLiked,
                targetUserId: post.UserId,
                postId: post.Id);

            await _notificationService.QueueAsync(
                new NotificationCreateRequest
                {
                    RecipientUserId = post.UserId,
                    ActorUserId = userId,
                    Type = NotificationType.PostLiked,
                    Priority = NotificationPriority.Low,
                    EntityType = NotificationEntityType.Post,
                    EntityId = post.Id.ToString(),
                    Title = "Postunuz bəyənildi",
                    Message = "Postunuzu bəyəndi.",
                    ActionUrl = "/"
                },
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UnlikeAsync(long postId, Guid userId, CancellationToken cancellationToken = default)
        {
            var post = await GetAccessiblePostAsync(postId, userId, cancellationToken);
            var like = await _context.PostLikes
                .FirstOrDefaultAsync(x => x.PostId == postId && x.UserId == userId, cancellationToken);

            if (like is null)
                return;

            _context.PostLikes.Remove(like);

            _context.AddUserActivity(
                userId,
                UserActivityType.PostUnliked,
                targetUserId: post.UserId,
                postId: post.Id);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<PagedResultDTO<InteractionLikeUserDTO>> GetLikesAsync(
            long postId,
            int pageNumber,
            int pageSize,
            Guid? currentUserId,
            CancellationToken cancellationToken = default)
        {
            ValidatePagination(pageNumber, pageSize);
            _ = await GetAccessiblePostAsync(postId, currentUserId, cancellationToken);

            var query = _context.PostLikes
                .AsNoTracking()
                .Where(x => x.PostId == postId);

            if (currentUserId.HasValue)
            {
                query = query.Where(x => !_context.UserBlocks.Any(block =>
                    (block.BlockerId == currentUserId.Value && block.BlockedUserId == x.UserId) ||
                    (block.BlockerId == x.UserId && block.BlockedUserId == currentUserId.Value)));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenBy(x => x.User.UserName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new InteractionLikeUserDTO
                {
                    UserId = x.UserId,
                    UserName = x.User.UserName ?? string.Empty,
                    ProfileImageUrl = x.User.ProfileImageUrl,
                    LikedAt = x.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return CreatePagedResult(items, totalCount, pageNumber, pageSize);
        }

        public async Task<PostCommentDTO> CreateCommentAsync(
            long postId,
            Guid userId,
            InteractionCommentCreateDTO dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            var post = await GetAccessiblePostAsync(postId, userId, cancellationToken);
            var content = dto.Content.NormalizeRequiredText("Şərh mətni", MaxCommentLength);

            var comment = new PostComment
            {
                PostId = postId,
                UserId = userId,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

            await _context.PostComments.AddAsync(comment, cancellationToken);

            _context.AddUserActivity(
                userId,
                UserActivityType.PostCommented,
                targetUserId: post.UserId,
                postId: post.Id);

            await _notificationService.QueueAsync(
                new NotificationCreateRequest
                {
                    RecipientUserId = post.UserId,
                    ActorUserId = userId,
                    Type = NotificationType.PostCommented,
                    EntityType = NotificationEntityType.Post,
                    EntityId = post.Id.ToString(),
                    Title = "Postunuza şərh yazıldı",
                    Message = "Postunuza şərh yazdı.",
                    ActionUrl = "/"
                },
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            return await GetCommentAsync(comment.Id, userId, cancellationToken);
        }

        public async Task<PagedResultDTO<PostCommentDTO>> GetCommentsAsync(
            long postId,
            InteractionCommentFilterDTO filter,
            Guid? currentUserId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ValidatePagination(filter.PageNumber, filter.PageSize);
            _ = await GetAccessiblePostAsync(postId, currentUserId, cancellationToken);

            var query = _context.PostComments
                .AsNoTracking()
                .Where(x => x.PostId == postId)
                .Where(x => !_context.UserBlocks.Any(block => currentUserId.HasValue &&
                    ((block.BlockerId == currentUserId.Value && block.BlockedUserId == x.UserId) ||
                     (block.BlockerId == x.UserId && block.BlockedUserId == currentUserId.Value))));

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(x => new PostCommentDTO
                {
                    Id = x.Id,
                    PostId = x.PostId,
                    UserId = x.UserId,
                    UserName = x.User.UserName ?? string.Empty,
                    ProfileImageUrl = x.User.ProfileImageUrl,
                    Content = x.Content,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    IsMine = currentUserId.HasValue && x.UserId == currentUserId.Value,
                    CanDelete = currentUserId.HasValue &&
                        (x.UserId == currentUserId.Value || x.Post.UserId == currentUserId.Value)
                })
                .ToListAsync(cancellationToken);

            return CreatePagedResult(items, totalCount, filter.PageNumber, filter.PageSize);
        }

        public async Task<PostCommentDTO> UpdateCommentAsync(
            long commentId,
            Guid userId,
            InteractionCommentUpdateDTO dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            ValidateCommentId(commentId);

            var comment = await _context.PostComments
                .FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken)
                ?? throw new NotFoundException("Şərh tapılmadı.");

            if (comment.UserId != userId)
                throw new ForbiddenException("Bu şərhi dəyişmək icazəniz yoxdur.");

            var post = await GetAccessiblePostAsync(comment.PostId, userId, cancellationToken);
            comment.Content = dto.Content.NormalizeRequiredText("Şərh mətni", MaxCommentLength);
            comment.UpdatedAt = DateTime.UtcNow;

            _context.AddUserActivity(
                userId,
                UserActivityType.PostCommentUpdated,
                targetUserId: post.UserId,
                postId: post.Id);

            await _context.SaveChangesAsync(cancellationToken);
            return await GetCommentAsync(comment.Id, userId, cancellationToken);
        }

        public async Task DeleteCommentAsync(long commentId, Guid userId, CancellationToken cancellationToken = default)
        {
            ValidateCommentId(commentId);

            var comment = await _context.PostComments
                .FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken)
                ?? throw new NotFoundException("Şərh tapılmadı.");

            var post = await GetAccessiblePostAsync(comment.PostId, userId, cancellationToken);

            if (comment.UserId != userId && post.UserId != userId)
                throw new ForbiddenException("Bu şərhi silmək icazəniz yoxdur.");

            comment.IsDeleted = true;
            comment.DeletedAtUtc = DateTime.UtcNow;
            comment.UpdatedAt = DateTime.UtcNow;

            _context.AddUserActivity(
                userId,
                UserActivityType.PostCommentDeleted,
                targetUserId: post.UserId,
                postId: post.Id);

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<PostCommentDTO> GetCommentAsync(long commentId, Guid currentUserId, CancellationToken cancellationToken)
        {
            return await _context.PostComments
                .AsNoTracking()
                .Where(x => x.Id == commentId)
                .Select(x => new PostCommentDTO
                {
                    Id = x.Id,
                    PostId = x.PostId,
                    UserId = x.UserId,
                    UserName = x.User.UserName ?? string.Empty,
                    ProfileImageUrl = x.User.ProfileImageUrl,
                    Content = x.Content,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    IsMine = x.UserId == currentUserId,
                    CanDelete = x.UserId == currentUserId || x.Post.UserId == currentUserId
                })
                .FirstAsync(cancellationToken);
        }

        private async Task<UserPost> GetAccessiblePostAsync(long postId, Guid? currentUserId, CancellationToken cancellationToken)
        {
            if (postId <= 0)
                throw new BadRequestException("Post ID-si düzgün deyil.");

            var post = await _context.UserPosts
                .AsNoTracking()
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Id == postId, cancellationToken)
                ?? throw new NotFoundException("Post tapılmadı.");

            if (currentUserId.HasValue)
            {
                var blocked = await _context.UserBlocks.AnyAsync(x =>
                    (x.BlockerId == currentUserId.Value && x.BlockedUserId == post.UserId) ||
                    (x.BlockerId == post.UserId && x.BlockedUserId == currentUserId.Value), cancellationToken);

                if (blocked)
                    throw new NotFoundException("Post tapılmadı.");
            }

            if (!post.User.IsPrivate || currentUserId == post.UserId)
                return post;

            if (!currentUserId.HasValue)
                throw new NotFoundException("Post tapılmadı.");

            var follows = await _context.UserFollows.AnyAsync(x =>
                x.FollowerId == currentUserId.Value &&
                x.FollowingId == post.UserId &&
                x.Status == FollowStatus.Accepted, cancellationToken);

            if (!follows)
                throw new NotFoundException("Post tapılmadı.");

            return post;
        }

        private static void ValidateCommentId(long commentId)
        {
            if (commentId <= 0)
                throw new BadRequestException("Şərh ID-si düzgün deyil.");
        }

        private static void ValidatePagination(int pageNumber, int pageSize)
        {
            if (pageNumber < 1)
                throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");

            if (pageSize < 1 || pageSize > MaxPageSize)
                throw new BadRequestException($"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");
        }

        private static PagedResultDTO<T> CreatePagedResult<T>(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
        {
            return new PagedResultDTO<T>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }
    }
}
