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

public sealed class GameListInteractionService : IGameListInteractionService
{
    private const int MaxCommentLength = 2000;
    private const int MaxPageSize = 100;
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;

    public GameListInteractionService(
        AppDbContext context,
        INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task LikeAsync(int gameListId, Guid userId, CancellationToken cancellationToken = default)
    {
        var gameList = await GetAccessibleGameListAsync(gameListId, userId, cancellationToken);

        if (gameList.UserId == userId)
            throw new BadRequestException("Öz siyahınızı bəyənə bilməzsiniz.");

        var exists = await _context.GameListLikes
            .AnyAsync(x => x.GameListId == gameListId && x.UserId == userId, cancellationToken);

        if (exists)
            return;

        await _context.GameListLikes.AddAsync(new GameListLike
        {
            GameListId = gameListId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        _context.AddUserActivity(
            userId,
            UserActivityType.GameListLiked,
            targetUserId: gameList.UserId,
            gameListId: gameList.Id,
            subjectTitle: gameList.Name);

        await _notificationService.QueueAsync(
            new NotificationCreateRequest
            {
                RecipientUserId = gameList.UserId,
                ActorUserId = userId,
                Type = NotificationType.GameListLiked,
                Priority = NotificationPriority.Low,
                EntityType = NotificationEntityType.GameList,
                EntityId = gameList.Id.ToString(),
                Title = "Siyahınız bəyənildi",
                Message = "Siyahınızı bəyəndi.",
                ActionUrl = $"/lists/{gameList.Id}"
            },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UnlikeAsync(int gameListId, Guid userId, CancellationToken cancellationToken = default)
    {
        var gameList = await GetAccessibleGameListAsync(gameListId, userId, cancellationToken);
        var like = await _context.GameListLikes
            .FirstOrDefaultAsync(x => x.GameListId == gameListId && x.UserId == userId, cancellationToken);

        if (like is null)
            return;

        _context.GameListLikes.Remove(like);

        _context.AddUserActivity(
            userId,
            UserActivityType.GameListUnliked,
            targetUserId: gameList.UserId,
            gameListId: gameList.Id,
            subjectTitle: gameList.Name);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResultDTO<InteractionLikeUserDTO>> GetLikesAsync(
        int gameListId,
        int pageNumber,
        int pageSize,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(pageNumber, pageSize);
        _ = await GetAccessibleGameListAsync(gameListId, currentUserId, cancellationToken);

        var query = _context.GameListLikes
            .AsNoTracking()
            .Where(x => x.GameListId == gameListId);

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

    public async Task<GameListCommentDTO> CreateCommentAsync(
        int gameListId,
        Guid userId,
        InteractionCommentCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var gameList = await GetAccessibleGameListAsync(gameListId, userId, cancellationToken);
        var content = dto.Content.NormalizeRequiredText("Şərh mətni", MaxCommentLength);

        var comment = new GameListComment
        {
            GameListId = gameListId,
            UserId = userId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        await _context.GameListComments.AddAsync(comment, cancellationToken);

        _context.AddUserActivity(
            userId,
            UserActivityType.GameListCommented,
            targetUserId: gameList.UserId,
            gameListId: gameList.Id,
            subjectTitle: gameList.Name);

        await _notificationService.QueueAsync(
            new NotificationCreateRequest
            {
                RecipientUserId = gameList.UserId,
                ActorUserId = userId,
                Type = NotificationType.GameListCommented,
                EntityType = NotificationEntityType.GameList,
                EntityId = gameList.Id.ToString(),
                Title = "Siyahınıza şərh yazıldı",
                Message = "Siyahınıza şərh yazdı.",
                ActionUrl = $"/lists/{gameList.Id}"
            },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        return await GetCommentAsync(comment.Id, userId, cancellationToken);
    }

    public async Task<PagedResultDTO<GameListCommentDTO>> GetCommentsAsync(
        int gameListId,
        InteractionCommentFilterDTO filter,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ValidatePagination(filter.PageNumber, filter.PageSize);
        _ = await GetAccessibleGameListAsync(gameListId, currentUserId, cancellationToken);

        var query = _context.GameListComments
            .AsNoTracking()
            .Where(x => x.GameListId == gameListId)
            .Where(x => !_context.UserBlocks.Any(block => currentUserId.HasValue &&
                ((block.BlockerId == currentUserId.Value && block.BlockedUserId == x.UserId) ||
                 (block.BlockerId == x.UserId && block.BlockedUserId == currentUserId.Value))));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => new GameListCommentDTO
            {
                Id = x.Id,
                GameListId = x.GameListId,
                UserId = x.UserId,
                UserName = x.User.UserName ?? string.Empty,
                ProfileImageUrl = x.User.ProfileImageUrl,
                Content = x.Content,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                IsMine = currentUserId.HasValue && x.UserId == currentUserId.Value
            })
            .ToListAsync(cancellationToken);

        return CreatePagedResult(items, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<GameListCommentDTO> UpdateCommentAsync(
        long commentId,
        Guid userId,
        InteractionCommentUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ValidateCommentId(commentId);

        var comment = await _context.GameListComments
            .FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken)
            ?? throw new NotFoundException("Şərh tapılmadı.");

        if (comment.UserId != userId)
            throw new ForbiddenException("Bu şərhi dəyişmək icazəniz yoxdur.");

        var gameList = await GetAccessibleGameListAsync(comment.GameListId, userId, cancellationToken);
        comment.Content = dto.Content.NormalizeRequiredText("Şərh mətni", MaxCommentLength);
        comment.UpdatedAt = DateTime.UtcNow;

        _context.AddUserActivity(
            userId,
            UserActivityType.GameListCommentUpdated,
            targetUserId: gameList.UserId,
            gameListId: gameList.Id,
            subjectTitle: gameList.Name);

        await _context.SaveChangesAsync(cancellationToken);
        return await GetCommentAsync(comment.Id, userId, cancellationToken);
    }

    public async Task DeleteCommentAsync(long commentId, Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateCommentId(commentId);

        var comment = await _context.GameListComments
            .FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken)
            ?? throw new NotFoundException("Şərh tapılmadı.");

        if (comment.UserId != userId)
            throw new ForbiddenException("Bu şərhi silmək icazəniz yoxdur.");

        var gameList = await GetAccessibleGameListAsync(comment.GameListId, userId, cancellationToken);
        comment.IsDeleted = true;
        comment.DeletedAtUtc = DateTime.UtcNow;
        comment.UpdatedAt = DateTime.UtcNow;

        _context.AddUserActivity(
            userId,
            UserActivityType.GameListCommentDeleted,
            targetUserId: gameList.UserId,
            gameListId: gameList.Id,
            subjectTitle: gameList.Name);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<GameListCommentDTO> GetCommentAsync(long commentId, Guid currentUserId, CancellationToken cancellationToken)
    {
        return await _context.GameListComments
            .AsNoTracking()
            .Where(x => x.Id == commentId)
            .Select(x => new GameListCommentDTO
            {
                Id = x.Id,
                GameListId = x.GameListId,
                UserId = x.UserId,
                UserName = x.User.UserName ?? string.Empty,
                ProfileImageUrl = x.User.ProfileImageUrl,
                Content = x.Content,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                IsMine = x.UserId == currentUserId
            })
            .FirstAsync(cancellationToken);
    }

    private async Task<GameList> GetAccessibleGameListAsync(int gameListId, Guid? currentUserId, CancellationToken cancellationToken)
    {
        if (gameListId <= 0)
            throw new BadRequestException("Siyahı ID-si düzgün deyil.");

        var gameList = await _context.GameLists
            .AsNoTracking()
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == gameListId, cancellationToken)
            ?? throw new NotFoundException("Oyun siyahısı tapılmadı.");

        if (currentUserId.HasValue && currentUserId.Value != gameList.UserId)
        {
            var blocked = await _context.UserBlocks.AnyAsync(x =>
                (x.BlockerId == currentUserId.Value && x.BlockedUserId == gameList.UserId) ||
                (x.BlockerId == gameList.UserId && x.BlockedUserId == currentUserId.Value), cancellationToken);

            if (blocked)
                throw new NotFoundException("Oyun siyahısı tapılmadı.");
        }

        if (currentUserId == gameList.UserId || gameList.Visibility == ListVisibility.Public)
            return gameList;

        if (gameList.Visibility == ListVisibility.Private || !currentUserId.HasValue)
            throw new NotFoundException("Oyun siyahısı tapılmadı.");

        var follows = await _context.UserFollows.AnyAsync(x =>
            x.FollowerId == currentUserId.Value &&
            x.FollowingId == gameList.UserId &&
            x.Status == FollowStatus.Accepted, cancellationToken);

        if (!follows)
            throw new NotFoundException("Oyun siyahısı tapılmadı.");

        return gameList;
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
