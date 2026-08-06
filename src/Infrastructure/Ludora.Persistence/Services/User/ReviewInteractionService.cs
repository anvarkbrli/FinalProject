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
    public sealed class ReviewInteractionService : IReviewInteractionService
    {
        private const int MaxCommentLength = 2000;
        private const int MaxPageSize = 100;
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;

        public ReviewInteractionService(
            AppDbContext context,
            INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task LikeAsync(long reviewId, Guid userId, CancellationToken cancellationToken = default)
        {
            var review = await GetAccessibleReviewAsync(reviewId, userId, cancellationToken);
            if (review.UserId == userId)
                throw new BadRequestException("Öz rəyinizi bəyənə bilməzsiniz.");

            var exists = await _context.ReviewLikes.AnyAsync(x => x.ReviewId == reviewId && x.UserId == userId, cancellationToken);
            if (exists) return;

            await _context.ReviewLikes.AddAsync(new ReviewLike
            {
                ReviewId = reviewId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            _context.AddUserActivity(
                userId,
                UserActivityType.ReviewLiked,
                targetUserId: review.UserId,
                gameSource: review.Source,
                gameId: review.GameId,
                reviewId: review.Id);

            await _notificationService.QueueAsync(
                new NotificationCreateRequest
                {
                    RecipientUserId = review.UserId,
                    ActorUserId = userId,
                    Type = NotificationType.ReviewLiked,
                    Priority = NotificationPriority.Low,
                    EntityType = NotificationEntityType.Review,
                    EntityId = review.Id.ToString(),
                    Title = "Rəyiniz bəyənildi",
                    Message = "Rəyinizi bəyəndi.",
                    ActionUrl = $"/games/{review.Source}/{review.GameId}#reviews"
                },
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UnlikeAsync(long reviewId, Guid userId, CancellationToken cancellationToken = default)
        {
            var review = await GetAccessibleReviewAsync(reviewId, userId, cancellationToken);
            var like = await _context.ReviewLikes.FirstOrDefaultAsync(x => x.ReviewId == reviewId && x.UserId == userId, cancellationToken);
            if (like is null) return;
            _context.ReviewLikes.Remove(like);

            _context.AddUserActivity(
                userId,
                UserActivityType.ReviewUnliked,
                targetUserId: review.UserId,
                gameSource: review.Source,
                gameId: review.GameId,
                reviewId: review.Id);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<PagedResultDTO<ReviewLikeUserDTO>> GetLikesAsync(
            long reviewId,
            int pageNumber,
            int pageSize,
            Guid? currentUserId,
            CancellationToken cancellationToken = default)
        {
            ValidatePagination(pageNumber, pageSize);
            _ = await GetAccessibleReviewAsync(reviewId, currentUserId, cancellationToken);

            var query = _context.ReviewLikes
                .AsNoTracking()
                .Where(x => x.ReviewId == reviewId);

            if (currentUserId.HasValue)
            {
                query = query.Where(x => !_context.UserBlocks.Any(block =>
                    block.BlockerId == currentUserId.Value && block.BlockedUserId == x.UserId ||
                    block.BlockerId == x.UserId && block.BlockedUserId == currentUserId.Value));
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenBy(x => x.User.UserName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ReviewLikeUserDTO
                {
                    UserId = x.UserId,
                    UserName = x.User.UserName ?? string.Empty,
                    ProfileImageUrl = x.User.ProfileImageUrl,
                    LikedAt = x.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new PagedResultDTO<ReviewLikeUserDTO>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0
                    ? 0
                    : (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        public async Task<ReviewCommentDTO> CreateCommentAsync(long reviewId, Guid userId, ReviewCommentCreateDTO dto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            var review = await GetAccessibleReviewAsync(reviewId, userId, cancellationToken);
            var content = dto.Content.NormalizeRequiredText("Şərh mətni", MaxCommentLength);

            var comment = new ReviewComment
            {
                ReviewId = reviewId,
                UserId = userId,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };
            await _context.ReviewComments.AddAsync(comment, cancellationToken);

            _context.AddUserActivity(
                userId,
                UserActivityType.ReviewCommented,
                targetUserId: review.UserId,
                gameSource: review.Source,
                gameId: review.GameId,
                reviewId: review.Id);

            await _notificationService.QueueAsync(
                new NotificationCreateRequest
                {
                    RecipientUserId = review.UserId,
                    ActorUserId = userId,
                    Type = NotificationType.ReviewCommented,
                    EntityType = NotificationEntityType.Review,
                    EntityId = review.Id.ToString(),
                    Title = "Rəyinizə cavab yazıldı",
                    Message = "Rəyinizə cavab yazdı.",
                    ActionUrl = $"/games/{review.Source}/{review.GameId}#reviews"
                },
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            return await GetCommentAsync(comment.Id, userId, cancellationToken);
        }

        public async Task<PagedResultDTO<ReviewCommentDTO>> GetCommentsAsync(long reviewId, ReviewCommentFilterDTO filter, Guid? currentUserId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
            ValidateFilter(filter);
            _ = await GetAccessibleReviewAsync(reviewId, currentUserId, cancellationToken);

            var query = _context.ReviewComments.AsNoTracking()
                .Where(x => x.ReviewId == reviewId)
                .Where(x => !_context.UserBlocks.Any(b => currentUserId.HasValue &&
                    (b.BlockerId == currentUserId.Value && b.BlockedUserId == x.UserId ||
                     b.BlockerId == x.UserId && b.BlockedUserId == currentUserId.Value)));

            var total = await query.CountAsync(cancellationToken);
            var items = await query.Include(x => x.User)
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(x => new ReviewCommentDTO
                {
                    Id = x.Id,
                    ReviewId = x.ReviewId,
                    UserId = x.UserId,
                    UserName = x.User.UserName ?? string.Empty,
                    ProfileImageUrl = x.User.ProfileImageUrl,
                    Content = x.Content,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    IsMine = currentUserId.HasValue && x.UserId == currentUserId.Value
                }).ToListAsync(cancellationToken);

            return new PagedResultDTO<ReviewCommentDTO>
            {
                Items = items,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalCount = total,
                TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)filter.PageSize)
            };
        }

        public async Task<ReviewCommentDTO> UpdateCommentAsync(long commentId, Guid userId, ReviewCommentUpdateDTO dto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);
            if (commentId <= 0) throw new BadRequestException("Şərh ID-si düzgün deyil.");
            var comment = await _context.ReviewComments.FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken)
                ?? throw new NotFoundException("Şərh tapılmadı.");
            if (comment.UserId != userId) throw new ForbiddenException("Bu şərhi dəyişmək icazəniz yoxdur.");
            var review = await GetAccessibleReviewAsync(comment.ReviewId, userId, cancellationToken);
            comment.Content = dto.Content.NormalizeRequiredText("Şərh mətni", MaxCommentLength);
            comment.UpdatedAt = DateTime.UtcNow;

            _context.AddUserActivity(
                userId,
                UserActivityType.ReviewCommentUpdated,
                targetUserId: review.UserId,
                gameSource: review.Source,
                gameId: review.GameId,
                reviewId: review.Id);

            await _context.SaveChangesAsync(cancellationToken);
            return await GetCommentAsync(comment.Id, userId, cancellationToken);
        }

        public async Task DeleteCommentAsync(long commentId, Guid userId, CancellationToken cancellationToken = default)
        {
            if (commentId <= 0) throw new BadRequestException("Şərh ID-si düzgün deyil.");
            var comment = await _context.ReviewComments.FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken)
                ?? throw new NotFoundException("Şərh tapılmadı.");
            if (comment.UserId != userId) throw new ForbiddenException("Bu şərhi silmək icazəniz yoxdur.");
            var review = await GetAccessibleReviewAsync(comment.ReviewId, userId, cancellationToken);
            comment.IsDeleted = true;
            comment.DeletedAtUtc = DateTime.UtcNow;
            comment.UpdatedAt = DateTime.UtcNow;

            _context.AddUserActivity(
                userId,
                UserActivityType.ReviewCommentDeleted,
                targetUserId: review.UserId,
                gameSource: review.Source,
                gameId: review.GameId,
                reviewId: review.Id);

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<ReviewCommentDTO> GetCommentAsync(long commentId, Guid currentUserId, CancellationToken cancellationToken)
        {
            return await _context.ReviewComments.AsNoTracking().Include(x => x.User)
                .Where(x => x.Id == commentId)
                .Select(x => new ReviewCommentDTO
                {
                    Id = x.Id, ReviewId = x.ReviewId, UserId = x.UserId,
                    UserName = x.User.UserName ?? string.Empty, ProfileImageUrl = x.User.ProfileImageUrl,
                    Content = x.Content, CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt,
                    IsMine = x.UserId == currentUserId
                }).FirstAsync(cancellationToken);
        }

        private async Task<Review> GetAccessibleReviewAsync(long reviewId, Guid? currentUserId, CancellationToken cancellationToken)
        {
            if (reviewId <= 0) throw new BadRequestException("Rəy ID-si düzgün deyil.");
            var review = await _context.Reviews.AsNoTracking().Include(x => x.User)
                .FirstOrDefaultAsync(
                    x => x.Id == reviewId && x.Content != string.Empty,
                    cancellationToken)
                ?? throw new NotFoundException("Rəy tapılmadı.");

            if (currentUserId.HasValue)
            {
                var blocked = await _context.UserBlocks.AnyAsync(x =>
                    x.BlockerId == currentUserId.Value && x.BlockedUserId == review.UserId ||
                    x.BlockerId == review.UserId && x.BlockedUserId == currentUserId.Value, cancellationToken);
                if (blocked) throw new NotFoundException("Rəy tapılmadı.");
            }

            if (!review.User.IsPrivate || currentUserId == review.UserId) return review;
            if (!currentUserId.HasValue) throw new NotFoundException("Rəy tapılmadı.");
            var follows = await _context.UserFollows.AnyAsync(x => x.FollowerId == currentUserId.Value && x.FollowingId == review.UserId && x.Status == FollowStatus.Accepted, cancellationToken);
            if (!follows) throw new NotFoundException("Rəy tapılmadı.");
            return review;
        }

        private static void ValidateFilter(ReviewCommentFilterDTO filter)
        {
            ValidatePagination(filter.PageNumber, filter.PageSize);
        }

        private static void ValidatePagination(int pageNumber, int pageSize)
        {
            if (pageNumber < 1)
                throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");

            if (pageSize < 1 || pageSize > MaxPageSize)
                throw new BadRequestException($"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");
        }
    }
}
