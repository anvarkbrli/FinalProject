using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Ludora.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class UserGameService : IUserGameService
{
    private const int MaxPageSize = 50;
    private readonly AppDbContext _context;
    private readonly IIgdbClient _igdbClient;

    public UserGameService(
        AppDbContext context,
        IIgdbClient igdbClient)
    {
        _context = context;
        _igdbClient = igdbClient;
    }

    public async Task<IReadOnlyList<UserGameDTO>> GetMineAsync(
        Guid userId,
        UserGameStatus? status = null,
        bool? isFavorite = null,
        bool? isWishlisted = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.UserGames
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (isFavorite.HasValue)
            query = query.Where(x => x.IsFavorite == isFavorite.Value);

        if (isWishlisted.HasValue)
            query = query.Where(x => x.IsWishlisted == isWishlisted.Value);

        var entities = await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ToListAsync(cancellationToken);

        return await MapManyAsync(entities, cancellationToken);
    }

    public async Task<PagedResultDTO<UserGameDTO>> GetByUserAsync(
        Guid userId,
        UserGameFilterDTO filter,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ValidateFilter(filter);

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

        await EnsureCanViewUserGamesAsync(
            user.Id,
            user.IsPrivate,
            currentUserId,
            cancellationToken);

        var query = _context.UserGames
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (filter.IsFavorite.HasValue)
            query = query.Where(x => x.IsFavorite == filter.IsFavorite.Value);

        if (filter.IsWishlisted.HasValue)
            query = query.Where(x => x.IsWishlisted == filter.IsWishlisted.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var items = await MapManyAsync(entities, cancellationToken);

        return new PagedResultDTO<UserGameDTO>
        {
            Items = items,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)filter.PageSize)
        };
    }

    public async Task<UserGameDTO?> GetStateAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");

        var entity = await _context.UserGames
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.Source == source &&
                    x.GameId == gameId,
                cancellationToken);

        return entity is null
            ? null
            : await MapAsync(entity, cancellationToken);
    }

    public async Task<UserGameDTO> SetStatusAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        UserGameStatus status,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status))
            throw new BadRequestException("Oyun statusu yanlışdır.");

        await EnsureGameExistsAsync(source, gameId, cancellationToken);

        var entity = await FindAsync(
            userId,
            source,
            gameId,
            cancellationToken);

        if (entity is null)
        {
            entity = NewEntity(userId, source, gameId);
            entity.Status = status;
            await _context.UserGames.AddAsync(entity, cancellationToken);
        }
        else
        {
            if (entity.Status == status)
                return await MapAsync(entity, cancellationToken);

            entity.Status = status;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        _context.AddUserActivity(
            userId,
            UserActivityType.GameStatusChanged,
            gameSource: source,
            gameId: gameId,
            status: status);

        await SaveAsync(cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    public async Task<UserGameDTO?> RemoveStatusAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");

        var entity = await FindAsync(
            userId,
            source,
            gameId,
            cancellationToken);

        if (entity is null)
            return null;

        if (!entity.Status.HasValue)
            return await MapAsync(entity, cancellationToken);

        var previousStatus = entity.Status.Value;
        entity.Status = null;

        _context.AddUserActivity(
            userId,
            UserActivityType.GameStatusRemoved,
            gameSource: source,
            gameId: gameId,
            status: previousStatus);

        return await SaveOrDeleteAsync(entity, cancellationToken);
    }

    public async Task<UserGameDTO?> SetFavoriteAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        bool isFavorite,
        CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");

        var entity = await FindAsync(
            userId,
            source,
            gameId,
            cancellationToken);

        if (entity is null)
        {
            if (!isFavorite)
                return null;

            await EnsureGameExistsAsync(source, gameId, cancellationToken);
            entity = NewEntity(userId, source, gameId);
            entity.IsFavorite = true;
            await _context.UserGames.AddAsync(entity, cancellationToken);
        }
        else
        {
            if (entity.IsFavorite == isFavorite)
                return await MapAsync(entity, cancellationToken);

            entity.IsFavorite = isFavorite;
        }

        _context.AddUserActivity(
            userId,
            isFavorite
                ? UserActivityType.GameAddedToFavorites
                : UserActivityType.GameRemovedFromFavorites,
            gameSource: source,
            gameId: gameId);

        return await SaveOrDeleteAsync(entity, cancellationToken);
    }

    public async Task<UserGameDTO?> SetWishlistAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        bool isWishlisted,
        CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");

        var entity = await FindAsync(
            userId,
            source,
            gameId,
            cancellationToken);

        if (entity is null)
        {
            if (!isWishlisted)
                return null;

            await EnsureGameExistsAsync(source, gameId, cancellationToken);
            entity = NewEntity(userId, source, gameId);
            entity.IsWishlisted = true;
            await _context.UserGames.AddAsync(entity, cancellationToken);
        }
        else
        {
            if (entity.IsWishlisted == isWishlisted)
                return await MapAsync(entity, cancellationToken);

            entity.IsWishlisted = isWishlisted;
        }

        _context.AddUserActivity(
            userId,
            isWishlisted
                ? UserActivityType.GameAddedToWishlist
                : UserActivityType.GameRemovedFromWishlist,
            gameSource: source,
            gameId: gameId);

        return await SaveOrDeleteAsync(entity, cancellationToken);
    }

    public async Task<UserGameDTO?> SetRatingAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        double? rating,
        CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");
        ValidateRating(rating);

        var entity = await FindAsync(
            userId,
            source,
            gameId,
            cancellationToken);

        if (entity is null)
        {
            if (!rating.HasValue)
                return null;

            await EnsureGameExistsAsync(source, gameId, cancellationToken);
            entity = NewEntity(userId, source, gameId);
            entity.Rating = rating.Value;
            await _context.UserGames.AddAsync(entity, cancellationToken);
        }
        else
        {
            if (entity.Rating == rating)
                return await MapAsync(entity, cancellationToken);

            entity.Rating = rating;
        }

        _context.AddUserActivity(
            userId,
            rating.HasValue
                ? UserActivityType.GameRated
                : UserActivityType.GameRatingRemoved,
            gameSource: source,
            gameId: gameId,
            rating: rating);

        return await SaveOrDeleteAsync(entity, cancellationToken);
    }

    private async Task<UserGameDTO?> SaveOrDeleteAsync(
        UserGame entity,
        CancellationToken cancellationToken)
    {
        if (IsEmpty(entity))
        {
            _context.UserGames.Remove(entity);
            await SaveAsync(cancellationToken);
            return null;
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await SaveAsync(cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation())
        {
            throw new ConflictException(
                "Bu oyun üçün istifadəçi məlumatı artıq mövcuddur.");
        }
    }

    private Task<UserGame?> FindAsync(
        Guid userId,
        CatalogSource source,
        long gameId,
        CancellationToken cancellationToken)
    {
        return _context.UserGames.FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.Source == source &&
                x.GameId == gameId,
            cancellationToken);
    }

    private async Task EnsureGameExistsAsync(
        CatalogSource source,
        long gameId,
        CancellationToken cancellationToken)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");

        if (source == CatalogSource.Local)
        {
            if (gameId > int.MaxValue ||
                !await _context.Games.AnyAsync(
                    x => x.Id == (int)gameId,
                    cancellationToken))
            {
                throw new NotFoundException("Oyun tapılmadı.");
            }

            return;
        }

        if (source == CatalogSource.Igdb)
        {
            _ = await _igdbClient.GetGameByIdAsync(
                    gameId,
                    cancellationToken)
                ?? throw new NotFoundException("IGDB oyunu tapılmadı.");

            return;
        }

        throw new BadRequestException("Oyun mənbəyi yanlışdır.");
    }

    private async Task EnsureCanViewUserGamesAsync(
        Guid userId,
        bool isPrivate,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId == userId)
            return;

        if (currentUserId.HasValue)
        {
            var hasBlock = await _context.UserBlocks
                .AsNoTracking()
                .AnyAsync(x =>
                        x.BlockerId == currentUserId.Value &&
                        x.BlockedUserId == userId ||
                        x.BlockerId == userId &&
                        x.BlockedUserId == currentUserId.Value,
                    cancellationToken);

            if (hasBlock)
            {
                throw new ForbiddenException(
                    "Bu istifadəçinin oyunlarına baxmaq mümkün deyil.");
            }
        }

        if (!isPrivate)
            return;

        var isFollowing = currentUserId.HasValue &&
                          await _context.UserFollows
                              .AsNoTracking()
                              .AnyAsync(x =>
                                      x.FollowerId == currentUserId.Value &&
                                      x.FollowingId == userId &&
                                      x.Status == FollowStatus.Accepted,
                                  cancellationToken);

        if (!isFollowing)
            throw new ForbiddenException("Bu profil gizlidir.");
    }

    private async Task<IReadOnlyList<UserGameDTO>> MapManyAsync(
        List<UserGame> entities,
        CancellationToken cancellationToken)
    {
        var localIds = entities
            .Where(x =>
                x.Source == CatalogSource.Local &&
                x.GameId <= int.MaxValue)
            .Select(x => (int)x.GameId)
            .Distinct()
            .ToList();

        var localGames = await _context.Games
            .AsNoTracking()
            .Where(x => localIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var result = new List<UserGameDTO>(entities.Count);

        foreach (var entity in entities)
        {
            if (entity.Source == CatalogSource.Local &&
                localGames.TryGetValue((int)entity.GameId, out var localGame))
            {
                result.Add(Map(
                    entity,
                    localGame.Name,
                    localGame.Slug,
                    localGame.CoverImageUrl,
                    localGame.ReleaseDate));
            }
            else if (entity.Source == CatalogSource.Igdb)
            {
                var game = await _igdbClient.GetGameByIdAsync(
                    entity.GameId,
                    cancellationToken);

                if (game is not null)
                {
                    result.Add(Map(
                        entity,
                        game.Name,
                        null,
                        game.CoverImageUrl,
                        game.ReleaseDate));
                }
            }
        }

        return result;
    }

    private async Task<UserGameDTO> MapAsync(
        UserGame entity,
        CancellationToken cancellationToken)
    {
        if (entity.Source == CatalogSource.Local)
        {
            var game = await _context.Games
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == (int)entity.GameId,
                    cancellationToken)
                ?? throw new NotFoundException("Oyun tapılmadı.");

            return Map(
                entity,
                game.Name,
                game.Slug,
                game.CoverImageUrl,
                game.ReleaseDate);
        }

        var igdbGame = await _igdbClient.GetGameByIdAsync(
                entity.GameId,
                cancellationToken)
            ?? throw new NotFoundException("IGDB oyunu tapılmadı.");

        return Map(
            entity,
            igdbGame.Name,
            null,
            igdbGame.CoverImageUrl,
            igdbGame.ReleaseDate);
    }

    private static UserGameDTO Map(
        UserGame entity,
        string name,
        string? slug,
        string? coverImageUrl,
        DateOnly? releaseDate)
    {
        return new UserGameDTO
        {
            Source = entity.Source,
            GameId = entity.GameId,
            Name = name,
            Slug = slug,
            CoverUrl = coverImageUrl,
            ReleaseDate = releaseDate,
            Status = entity.Status,
            IsFavorite = entity.IsFavorite,
            IsWishlisted = entity.IsWishlisted,
            Rating = entity.Rating,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static UserGame NewEntity(
        Guid userId,
        CatalogSource source,
        long gameId)
    {
        return new UserGame
        {
            UserId = userId,
            Source = source,
            GameId = gameId,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static bool IsEmpty(UserGame entity)
    {
        return entity.Status is null &&
               !entity.IsFavorite &&
               !entity.IsWishlisted &&
               !entity.Rating.HasValue;
    }

    private static void ValidateFilter(UserGameFilterDTO filter)
    {
        if (filter.PageNumber < 1)
            throw new BadRequestException("Səhifə nömrəsi 1-dən kiçik ola bilməz.");

        if (filter.PageSize < 1 || filter.PageSize > MaxPageSize)
        {
            throw new BadRequestException(
                $"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");
        }
    }

    private static void ValidateRating(double? rating)
    {
        if (!rating.HasValue)
            return;

        var value = rating.Value;
        var isHalfStep = Math.Abs(
            value * 2 - Math.Round(value * 2)) < 0.000001;

        if (value < 0.5 || value > 5 || !isHalfStep)
        {
            throw new BadRequestException(
                "Reytinq 0.5 ilə 5 arasında və 0.5 addımlarla olmalıdır.");
        }
    }
}
