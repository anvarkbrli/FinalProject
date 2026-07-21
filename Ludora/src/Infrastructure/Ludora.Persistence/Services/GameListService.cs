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

public sealed class GameListService : IGameListService
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 1000;

    private readonly AppDbContext _context;
    private readonly IGameService _gameService;
    private readonly INotificationService _notificationService;

    public GameListService(
        AppDbContext context,
        IGameService gameService,
        INotificationService notificationService)
    {
        _context = context;
        _gameService = gameService;
        _notificationService = notificationService;
    }

    public async Task<IReadOnlyList<GameListSummaryDTO>> GetUserListsAsync(
        Guid userId,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, cancellationToken);

        if (!userExists)
            throw new NotFoundException("İstifadəçi tapılmadı.");

        await CheckBlockAccessAsync(userId, currentUserId, cancellationToken);

        var query = _context.GameLists
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (currentUserId != userId)
        {
            var isFollowing = currentUserId.HasValue &&
                              await _context.UserFollows
                                  .AsNoTracking()
                                  .AnyAsync(x =>
                                      x.FollowerId == currentUserId.Value &&
                                      x.FollowingId == userId &&
                                      x.Status == FollowStatus.Accepted,
                                      cancellationToken);

            query = isFollowing
                ? query.Where(x => x.Visibility != ListVisibility.Private)
                : query.Where(x => x.Visibility == ListVisibility.Public);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new GameListSummaryDTO
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Visibility = x.Visibility,
                IsRanked = x.IsRanked,
                OwnerId = x.UserId,
                OwnerUserName = x.User.UserName ?? string.Empty,
                GameCount = x.GameListItems.Count,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                LikeCount = x.Likes.Count,
                CommentCount = x.Comments.Count,
                IsLikedByCurrentUser = currentUserId.HasValue &&
                    x.Likes.Any(like => like.UserId == currentUserId.Value)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<GameListDetailDTO> GetByIdAsync(
        int id,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidId();

        var gameList = await _context.GameLists
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.GameListItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Oyun siyahısı tapılmadı.");

        await CheckListAccessAsync(gameList, currentUserId, cancellationToken);

        var likeCount = await _context.GameListLikes
            .AsNoTracking()
            .CountAsync(x => x.GameListId == id, cancellationToken);

        var commentCount = await _context.GameListComments
            .AsNoTracking()
            .CountAsync(x => x.GameListId == id, cancellationToken);

        var isLikedByCurrentUser = currentUserId.HasValue &&
            await _context.GameListLikes
                .AsNoTracking()
                .AnyAsync(x =>
                    x.GameListId == id &&
                    x.UserId == currentUserId.Value,
                    cancellationToken);

        var orderedItems = gameList.IsRanked
            ? gameList.GameListItems
                .OrderBy(x => x.Position ?? int.MaxValue)
                .ThenBy(x => x.AddedAt)
            : gameList.GameListItems
                .OrderByDescending(x => x.AddedAt)
                .ThenBy(x => x.GameTitle);

        return new GameListDetailDTO
        {
            Id = gameList.Id,
            Name = gameList.Name,
            Description = gameList.Description,
            Visibility = gameList.Visibility,
            IsRanked = gameList.IsRanked,
            OwnerId = gameList.UserId,
            OwnerUserName = gameList.User.UserName ?? string.Empty,
            IsOwner = currentUserId == gameList.UserId,
            GameCount = gameList.GameListItems.Count,
            CreatedAt = gameList.CreatedAt,
            UpdatedAt = gameList.UpdatedAt,
            LikeCount = likeCount,
            CommentCount = commentCount,
            IsLikedByCurrentUser = isLikedByCurrentUser,
            Games = orderedItems
                .Select(x => new GameListEntryDTO
                {
                    Source = x.GameSource,
                    Id = x.GameSourceId,
                    Title = x.GameTitle,
                    CoverImageUrl = x.CoverImageUrl,
                    ReleaseDate = x.ReleaseDate,
                    AverageRating = x.Rating,
                    Position = x.Position,
                    AddedAt = x.AddedAt
                })
                .ToList()
        };
    }

    public async Task<int> CreateAsync(
        Guid userId,
        GameListCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        dto.Visibility.EnsureDefined("Siyahının görünmə tipi");

        var userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, cancellationToken);

        if (!userExists)
            throw new NotFoundException("İstifadəçi tapılmadı.");

        var name = dto.Name.NormalizeName(MaxNameLength, "Siyahı");
        await EnsureListNameIsAvailableAsync(userId, name, null, cancellationToken);

        var gameList = new GameList
        {
            Name = name,
            Description = dto.Description.NormalizeOptionalText(
                "Siyahının açıqlaması",
                MaxDescriptionLength),
            Visibility = dto.Visibility,
            IsRanked = dto.IsRanked,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        await _context.GameLists.AddAsync(gameList, cancellationToken);
        await SaveListChangesAsync(cancellationToken);

        _context.AddUserActivity(
            userId,
            UserActivityType.GameListCreated,
            gameListId: gameList.Id,
            subjectTitle: gameList.Name);

        if (gameList.Visibility == ListVisibility.Public)
        {
            await _notificationService.QueueForFollowersAsync(
                new FollowerNotificationRequest
                {
                    ActorUserId = userId,
                    Type = NotificationType.FollowingPublicListCreated,
                    EntityType = NotificationEntityType.GameList,
                    EntityId = gameList.Id.ToString(),
                    Title = "Yeni açıq siyahı",
                    Message = $"{gameList.Name} adlı yeni açıq siyahı yaratdı.",
                    ActionUrl = $"/lists/{gameList.Id}"
                },
                cancellationToken);
        }

        await SaveListChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return gameList.Id;
    }

    public async Task UpdateAsync(
        int id,
        Guid userId,
        GameListUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var gameList = await GetUserGameListAsync(
            id,
            userId,
            includeItems: true,
            cancellationToken);

        var hasChanges = false;

        if (dto.Name is not null)
        {
            var name = dto.Name.NormalizeName(MaxNameLength, "Siyahı");
            await EnsureListNameIsAvailableAsync(userId, name, id, cancellationToken);
            gameList.Name = name;
            hasChanges = true;
        }

        var description = dto.Description.NormalizeOptionalText(
            "Siyahının açıqlaması",
            MaxDescriptionLength);

        if (description != gameList.Description)
        {
            gameList.Description = description;
            hasChanges = true;
        }

        if (dto.Visibility.HasValue)
        {
            dto.Visibility.Value.EnsureDefined("Siyahının görünmə tipi");
            gameList.Visibility = dto.Visibility.Value;
            hasChanges = true;
        }

        if (dto.IsRanked.HasValue && dto.IsRanked.Value != gameList.IsRanked)
        {
            gameList.IsRanked = dto.IsRanked.Value;

            if (gameList.IsRanked)
            {
                var position = 1;

                foreach (var item in gameList.GameListItems
                             .OrderBy(x => x.AddedAt)
                             .ThenBy(x => x.GameSource)
                             .ThenBy(x => x.GameSourceId))
                {
                    item.Position = position++;
                }
            }
            else
            {
                foreach (var item in gameList.GameListItems)
                    item.Position = null;
            }

            hasChanges = true;
        }

        if (!hasChanges)
            return;

        gameList.UpdatedAt = DateTime.UtcNow;

        _context.AddUserActivity(
            userId,
            UserActivityType.GameListUpdated,
            gameListId: gameList.Id,
            subjectTitle: gameList.Name);

        await SaveListChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var gameList = await GetUserGameListAsync(
            id,
            userId,
            includeItems: false,
            cancellationToken);

        gameList.IsDeleted = true;
        gameList.UpdatedAt = DateTime.UtcNow;

        _context.AddUserActivity(
            userId,
            UserActivityType.GameListDeleted,
            gameListId: gameList.Id,
            subjectTitle: gameList.Name);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(
        int id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        id.EnsureValidId();

        var gameList = await _context.GameLists
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Oyun siyahısı tapılmadı.");

        if (!gameList.IsDeleted)
            return;

        await EnsureListNameIsAvailableAsync(
            userId,
            gameList.Name,
            gameList.Id,
            cancellationToken);

        gameList.IsDeleted = false;
        gameList.UpdatedAt = DateTime.UtcNow;
        await SaveListChangesAsync(cancellationToken);
    }

    public async Task AddGameAsync(
        int listId,
        CatalogSource source,
        long gameId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");

        var gameList = await GetUserGameListAsync(
            listId,
            userId,
            includeItems: false,
            cancellationToken);

        var itemExists = await _context.GameListItems
            .AsNoTracking()
            .AnyAsync(x =>
                x.GameListId == listId &&
                x.GameSource == source &&
                x.GameSourceId == gameId,
                cancellationToken);

        if (itemExists)
            throw new ConflictException("Bu oyun siyahıda artıq mövcuddur.");

        var game = await _gameService.GetByIdAsync(source, gameId, cancellationToken);

        int? position = null;

        if (gameList.IsRanked)
        {
            position = (await _context.GameListItems
                .Where(x => x.GameListId == listId)
                .MaxAsync(x => (int?)x.Position, cancellationToken) ?? 0) + 1;
        }

        var gameListItem = new GameListItem
        {
            GameListId = listId,
            GameSource = source,
            GameSourceId = gameId,
            GameTitle = game.Title,
            CoverImageUrl = game.CoverImageUrl,
            ReleaseDate = game.ReleaseDate,
            Rating = game.AverageRating,
            Position = position,
            AddedAt = DateTime.UtcNow
        };

        await _context.GameListItems.AddAsync(gameListItem, cancellationToken);
        gameList.UpdatedAt = DateTime.UtcNow;

        _context.AddUserActivity(
            userId,
            UserActivityType.GameAddedToList,
            gameSource: source,
            gameId: gameId,
            gameListId: gameList.Id,
            subjectTitle: gameList.Name);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
        {
            throw new ConflictException("Bu oyun siyahıda artıq mövcuddur.");
        }
    }

    public async Task RemoveGameAsync(
        int listId,
        CatalogSource source,
        long gameId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");

        var gameList = await GetUserGameListAsync(
            listId,
            userId,
            includeItems: false,
            cancellationToken);

        var item = await _context.GameListItems
            .FirstOrDefaultAsync(x =>
                x.GameListId == listId &&
                x.GameSource == source &&
                x.GameSourceId == gameId,
                cancellationToken)
            ?? throw new NotFoundException("Oyun bu siyahıda tapılmadı.");

        var removedPosition = item.Position;
        _context.GameListItems.Remove(item);

        if (gameList.IsRanked && removedPosition.HasValue)
        {
            var followingItems = await _context.GameListItems
                .Where(x =>
                    x.GameListId == listId &&
                    x.Position > removedPosition.Value)
                .ToListAsync(cancellationToken);

            foreach (var followingItem in followingItems)
                followingItem.Position--;
        }

        gameList.UpdatedAt = DateTime.UtcNow;

        _context.AddUserActivity(
            userId,
            UserActivityType.GameRemovedFromList,
            gameSource: source,
            gameId: gameId,
            gameListId: gameList.Id,
            subjectTitle: gameList.Name);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateGamePositionAsync(
        int listId,
        CatalogSource source,
        long gameId,
        int position,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");

        var gameList = await GetUserGameListAsync(
            listId,
            userId,
            includeItems: true,
            cancellationToken);

        if (!gameList.IsRanked)
            throw new BadRequestException("Yalnız sıralanmış siyahılarda oyunların mövqeyi dəyişdirilə bilər.");

        var orderedItems = gameList.GameListItems
            .OrderBy(x => x.Position ?? int.MaxValue)
            .ThenBy(x => x.AddedAt)
            .ToList();

        if (position < 1 || position > orderedItems.Count)
            throw new BadRequestException($"Mövqe 1-{orderedItems.Count} aralığında olmalıdır.");

        var item = orderedItems.FirstOrDefault(x =>
            x.GameSource == source &&
            x.GameSourceId == gameId)
            ?? throw new NotFoundException("Oyun bu siyahıda tapılmadı.");

        var oldPosition = item.Position
            ?? throw new BadRequestException("Oyunun mövqeyi müəyyən edilməyib.");

        if (oldPosition == position)
            return;

        if (position < oldPosition)
        {
            foreach (var otherItem in orderedItems.Where(x =>
                         x.Position >= position &&
                         x.Position < oldPosition))
            {
                otherItem.Position++;
            }
        }
        else
        {
            foreach (var otherItem in orderedItems.Where(x =>
                         x.Position > oldPosition &&
                         x.Position <= position))
            {
                otherItem.Position--;
            }
        }

        item.Position = position;
        gameList.UpdatedAt = DateTime.UtcNow;

        _context.AddUserActivity(
            userId,
            UserActivityType.GameListGameReordered,
            gameSource: source,
            gameId: gameId,
            gameListId: gameList.Id,
            subjectTitle: gameList.Name);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<GameList> GetUserGameListAsync(
        int id,
        Guid userId,
        bool includeItems,
        CancellationToken cancellationToken)
    {
        id.EnsureValidId();

        IQueryable<GameList> query = _context.GameLists;

        if (includeItems)
            query = query.Include(x => x.GameListItems);

        var gameList = await query
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Oyun siyahısı tapılmadı.");

        if (gameList.UserId != userId)
            throw new ForbiddenException("Bu oyun siyahısını dəyişmək icazəniz yoxdur.");

        return gameList;
    }

    private async Task CheckListAccessAsync(
        GameList gameList,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        await CheckBlockAccessAsync(gameList.UserId, currentUserId, cancellationToken);

        if (gameList.UserId == currentUserId ||
            gameList.Visibility == ListVisibility.Public)
        {
            return;
        }

        if (gameList.Visibility == ListVisibility.Private)
            throw new ForbiddenException("Bu oyun siyahısı gizlidir.");

        if (!currentUserId.HasValue)
            throw new ForbiddenException("Bu siyahını yalnız izləyicilər görə bilər.");

        var isFollowing = await _context.UserFollows
            .AsNoTracking()
            .AnyAsync(x =>
                x.FollowerId == currentUserId.Value &&
                x.FollowingId == gameList.UserId &&
                x.Status == FollowStatus.Accepted,
                cancellationToken);

        if (!isFollowing)
            throw new ForbiddenException("Bu siyahını yalnız izləyicilər görə bilər.");
    }

    private async Task CheckBlockAccessAsync(
        Guid ownerId,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        if (!currentUserId.HasValue || currentUserId.Value == ownerId)
            return;

        var hasBlock = await _context.UserBlocks
            .AsNoTracking()
            .AnyAsync(x =>
                (x.BlockerId == currentUserId.Value && x.BlockedUserId == ownerId) ||
                (x.BlockerId == ownerId && x.BlockedUserId == currentUserId.Value),
                cancellationToken);

        if (hasBlock)
            throw new ForbiddenException("Bu istifadəçinin oyun siyahılarına baxmaq mümkün deyil.");
    }

    private async Task EnsureListNameIsAvailableAsync(
        Guid userId,
        string name,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var exists = await _context.GameLists
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(x =>
                x.UserId == userId &&
                (!excludedId.HasValue || x.Id != excludedId.Value) &&
                x.Name == name,
                cancellationToken);

        if (exists)
            throw new ConflictException("Bu adda oyun siyahısı artıq mövcuddur.");
    }

    private async Task SaveListChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation())
        {
            throw new ConflictException("Bu adda oyun siyahısı artıq mövcuddur.");
        }
    }
}
