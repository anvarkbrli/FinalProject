using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models.Notification;
using Ludora.Domain.Entities.Forum;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class DiscussionReplyService : IDiscussionReplyService
{
    private const int MaxContentLength = 5000;
    private const int MaxPageSize = 100;

    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;

    public DiscussionReplyService(
        AppDbContext context,
        INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<DiscussionReplyDTO> GetByIdAsync(
        int replyId,
        Guid? currentUserId,
        bool canModerate,
        CancellationToken cancellationToken = default)
    {
        replyId.EnsureValidId("Cavab ID-si");

        var replyInfo = await _context.DiscussionReplies
            .AsNoTracking()
            .Where(x => x.Id == replyId)
            .Select(x => new
            {
                ReplyAuthorId = x.UserId,
                DiscussionAuthorId = x.Discussion.UserId
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Cavab tapılmadı.");

        if (currentUserId.HasValue && !canModerate)
        {
            await EnsureUsersAreNotBlockedAsync(
                currentUserId.Value,
                replyInfo.DiscussionAuthorId,
                cancellationToken);

            await EnsureUsersAreNotBlockedAsync(
                currentUserId.Value,
                replyInfo.ReplyAuthorId,
                cancellationToken);
        }

        return await ProjectReplies(
                _context.DiscussionReplies.AsNoTracking().Where(x => x.Id == replyId),
                currentUserId,
                canModerate)
            .FirstAsync(cancellationToken);
    }

    public async Task<PagedResultDTO<DiscussionReplyDTO>> GetByDiscussionAsync(
        int discussionId,
        int pageNumber,
        int pageSize,
        Guid? currentUserId,
        bool canModerate,
        CancellationToken cancellationToken = default)
    {
        discussionId.EnsureValidId("Mövzu ID-si");
        ValidatePagination(pageNumber, pageSize);

        var discussionAuthorId = await _context.Discussions
            .AsNoTracking()
            .Where(x => x.Id == discussionId)
            .Select(x => (Guid?)x.UserId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Mövzu tapılmadı.");

        if (currentUserId.HasValue && !canModerate)
        {
            await EnsureUsersAreNotBlockedAsync(
                currentUserId.Value,
                discussionAuthorId,
                cancellationToken);
        }

        var query = _context.DiscussionReplies
            .AsNoTracking()
            .Where(x => x.DiscussionId == discussionId);

        if (currentUserId.HasValue && !canModerate)
            query = ExcludeBlockedAuthors(query, currentUserId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await ProjectReplies(query, currentUserId, canModerate)
            .OrderBy(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return CreatePagedResult(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PagedResultDTO<DiscussionReplyDTO>> GetDeletedAsync(
        int? discussionId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (discussionId.HasValue)
            discussionId.Value.EnsureValidId("Mövzu ID-si");

        ValidatePagination(pageNumber, pageSize);

        var query = _context.DiscussionReplies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.IsDeleted);

        if (discussionId.HasValue)
            query = query.Where(x => x.DiscussionId == discussionId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await ProjectReplies(query, null, true)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return CreatePagedResult(items, pageNumber, pageSize, totalCount);
    }

    public async Task<DiscussionReplyDTO> CreateAsync(
        int discussionId,
        Guid userId,
        DiscussionReplyCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        discussionId.EnsureValidId("Mövzu ID-si");
        ArgumentNullException.ThrowIfNull(dto);

        var discussion = await _context.Discussions
            .AsNoTracking()
            .Where(x => x.Id == discussionId)
            .Select(x => new
            {
                x.UserId,
                x.IsLocked
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Mövzu tapılmadı.");

        if (discussion.IsLocked)
            throw new ConflictException("Bu mövzu kilidlənib və yeni cavab qəbul etmir.");

        await EnsureUsersAreNotBlockedAsync(userId, discussion.UserId, cancellationToken);

        var content = dto.Content.NormalizeRequiredText("Cavab mətni", MaxContentLength);

        var reply = new DiscussionReply
        {
            DiscussionId = discussionId,
            UserId = userId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        await _context.DiscussionReplies.AddAsync(reply, cancellationToken);

        await _notificationService.QueueAsync(
            new NotificationCreateRequest
            {
                RecipientUserId = discussion.UserId,
                ActorUserId = userId,
                Type = NotificationType.DiscussionReplied,
                EntityType = NotificationEntityType.Discussion,
                EntityId = discussionId.ToString(),
                Title = "Müzakirənizə cavab yazıldı",
                Message = "Açdığınız müzakirəyə cavab yazdı.",
                ActionUrl = "/#forum"
            },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return await ProjectReplies(
                _context.DiscussionReplies.AsNoTracking().Where(x => x.Id == reply.Id),
                userId,
                false)
            .FirstAsync(cancellationToken);
    }

    public async Task<DiscussionReplyDTO> UpdateAsync(
        int replyId,
        Guid userId,
        bool canModerate,
        DiscussionReplyUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        replyId.EnsureValidId("Cavab ID-si");
        ArgumentNullException.ThrowIfNull(dto);

        var reply = await _context.DiscussionReplies
            .Include(x => x.Discussion)
            .FirstOrDefaultAsync(x => x.Id == replyId, cancellationToken)
            ?? throw new NotFoundException("Cavab tapılmadı.");

        EnsureCanModify(reply.UserId, userId, canModerate);

        if (reply.Discussion.IsLocked && !canModerate)
            throw new ConflictException("Kilidlənmiş mövzudakı cavabı dəyişmək mümkün deyil.");

        reply.Content = dto.Content.NormalizeRequiredText("Cavab mətni", MaxContentLength);
        reply.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return await ProjectReplies(
                _context.DiscussionReplies.AsNoTracking().Where(x => x.Id == reply.Id),
                userId,
                canModerate)
            .FirstAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int replyId,
        Guid userId,
        bool canModerate,
        CancellationToken cancellationToken = default)
    {
        replyId.EnsureValidId("Cavab ID-si");

        var reply = await _context.DiscussionReplies
            .FirstOrDefaultAsync(x => x.Id == replyId, cancellationToken)
            ?? throw new NotFoundException("Cavab tapılmadı.");

        EnsureCanModify(reply.UserId, userId, canModerate);

        reply.IsDeleted = true;
        reply.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task LikeAsync(
        int replyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        replyId.EnsureValidId("Cavab ID-si");

        var replyInfo = await _context.DiscussionReplies
            .AsNoTracking()
            .Where(x => x.Id == replyId)
            .Select(x => new
            {
                ReplyAuthorId = x.UserId,
                DiscussionAuthorId = x.Discussion.UserId
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Cavab tapılmadı.");

        await EnsureUsersAreNotBlockedAsync(
            userId,
            replyInfo.DiscussionAuthorId,
            cancellationToken);

        await EnsureUsersAreNotBlockedAsync(
            userId,
            replyInfo.ReplyAuthorId,
            cancellationToken);

        var alreadyLiked = await _context.DiscussionReplyLikes
            .AnyAsync(x => x.ReplyId == replyId && x.UserId == userId, cancellationToken);

        if (alreadyLiked)
            throw new ConflictException("Bu cavabı artıq bəyənmisiniz.");

        await _context.DiscussionReplyLikes.AddAsync(new DiscussionReplyLike
        {
            ReplyId = replyId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _notificationService.QueueAsync(
            new NotificationCreateRequest
            {
                RecipientUserId = replyInfo.ReplyAuthorId,
                ActorUserId = userId,
                Type = NotificationType.DiscussionReplyLiked,
                Priority = NotificationPriority.Low,
                EntityType = NotificationEntityType.DiscussionReply,
                EntityId = replyId.ToString(),
                Title = "Cavabınız bəyənildi",
                Message = "Müzakirədəki cavabınızı bəyəndi.",
                ActionUrl = "/#forum"
            },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UnlikeAsync(
        int replyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        replyId.EnsureValidId("Cavab ID-si");

        var like = await _context.DiscussionReplyLikes
            .FirstOrDefaultAsync(
                x => x.ReplyId == replyId && x.UserId == userId,
                cancellationToken)
            ?? throw new NotFoundException("Cavab bəyənməsi tapılmadı.");

        _context.DiscussionReplyLikes.Remove(like);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DiscussionReplyDTO> RestoreAsync(
        int replyId,
        CancellationToken cancellationToken = default)
    {
        replyId.EnsureValidId("Cavab ID-si");

        var reply = await _context.DiscussionReplies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == replyId, cancellationToken)
            ?? throw new NotFoundException("Cavab tapılmadı.");

        var discussionIsActive = await _context.Discussions
            .AsNoTracking()
            .AnyAsync(x => x.Id == reply.DiscussionId, cancellationToken);

        if (!discussionIsActive)
        {
            throw new ConflictException(
                "Cavabın aid olduğu mövzu silinib. Əvvəlcə mövzunu bərpa edin.");
        }

        if (reply.IsDeleted)
        {
            reply.IsDeleted = false;
            reply.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await ProjectReplies(
                _context.DiscussionReplies.AsNoTracking().Where(x => x.Id == replyId),
                null,
                true)
            .FirstAsync(cancellationToken);
    }

    private IQueryable<DiscussionReply> ExcludeBlockedAuthors(
        IQueryable<DiscussionReply> query,
        Guid currentUserId)
    {
        var blockedUserIds = _context.UserBlocks
            .AsNoTracking()
            .Where(x => x.BlockerId == currentUserId)
            .Select(x => x.BlockedUserId)
            .Concat(
                _context.UserBlocks
                    .AsNoTracking()
                    .Where(x => x.BlockedUserId == currentUserId)
                    .Select(x => x.BlockerId));

        return query.Where(x => !blockedUserIds.Contains(x.UserId));
    }

    private static IQueryable<DiscussionReplyDTO> ProjectReplies(
        IQueryable<DiscussionReply> query,
        Guid? currentUserId,
        bool canModerate)
    {
        return query.Select(x => new DiscussionReplyDTO
        {
            Id = x.Id,
            DiscussionId = x.DiscussionId,
            AuthorId = x.UserId,
            AuthorUserName = x.User.UserName ?? string.Empty,
            AuthorProfileImageUrl = x.User.ProfileImageUrl,
            Content = x.Content,
            LikeCount = x.Likes.Count,
            IsLikedByCurrentUser = currentUserId.HasValue &&
                                   x.Likes.Any(like => like.UserId == currentUserId.Value),
            IsDeleted = x.IsDeleted,
            CanEdit = currentUserId.HasValue &&
                      (canModerate || x.UserId == currentUserId.Value),
            CanDelete = currentUserId.HasValue &&
                        (canModerate || x.UserId == currentUserId.Value),
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        });
    }

    private async Task EnsureUsersAreNotBlockedAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken)
    {
        if (firstUserId == secondUserId)
            return;

        var isBlocked = await _context.UserBlocks
            .AsNoTracking()
            .AnyAsync(x =>
                    (x.BlockerId == firstUserId && x.BlockedUserId == secondUserId) ||
                    (x.BlockerId == secondUserId && x.BlockedUserId == firstUserId),
                cancellationToken);

        if (isBlocked)
            throw new ForbiddenException("Bloklanmış istifadəçilər arasında bu əməliyyat mümkün deyil.");
    }

    private static void EnsureCanModify(
        Guid ownerId,
        Guid currentUserId,
        bool canModerate)
    {
        if (ownerId != currentUserId && !canModerate)
            throw new ForbiddenException("Bu cavabı dəyişmək üçün icazəniz yoxdur.");
    }

    private static void ValidatePagination(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");

        if (pageSize is < 1 or > MaxPageSize)
            throw new BadRequestException($"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");

        if ((long)(pageNumber - 1) * pageSize > int.MaxValue)
            throw new BadRequestException("Səhifə parametrləri həddindən artıq böyükdür.");
    }

    private static PagedResultDTO<T> CreatePagedResult<T>(
        IReadOnlyList<T> items,
        int pageNumber,
        int pageSize,
        int totalCount)
    {
        return new PagedResultDTO<T>
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
}
