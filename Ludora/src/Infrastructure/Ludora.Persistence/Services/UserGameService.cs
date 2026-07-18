using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Application.Models;
using Ludora.Domain.Entities;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Ludora.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services;

public sealed class UserGameService : IUserGameService
{
    private readonly AppDbContext _context;
    private readonly IIgdbClient _igdbClient;

    public UserGameService(AppDbContext context, IIgdbClient igdbClient)
    {
        _context = context;
        _igdbClient = igdbClient;
    }

    public async Task<IReadOnlyList<UserGameDTO>> GetMineAsync(Guid userId, UserGameStatus? status = null,
        bool? isFavorite = null, bool? isWishlisted = null, CancellationToken cancellationToken = default)
    {
        var query = _context.UserGames.AsNoTracking().Where(x => x.UserId == userId);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (isFavorite.HasValue) query = query.Where(x => x.IsFavorite == isFavorite.Value);
        if (isWishlisted.HasValue) query = query.Where(x => x.IsWishlisted == isWishlisted.Value);

        var entities = await query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ToListAsync(cancellationToken);
        return await MapManyAsync(entities, cancellationToken);
    }

    public async Task<UserGameDTO?> GetStateAsync(Guid userId, CatalogSource source, long gameId,
        CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");
        var entity = await _context.UserGames.AsNoTracking().FirstOrDefaultAsync(x =>
            x.UserId == userId && x.Source == source && x.GameId == gameId, cancellationToken);
        return entity is null ? null : await MapAsync(entity, cancellationToken);
    }

    public async Task<UserGameDTO> SetStatusAsync(Guid userId, CatalogSource source, long gameId,
        UserGameStatus status, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status)) throw new BadRequestException("Oyun statusu yanlışdır.");
        await EnsureGameExistsAsync(source, gameId, cancellationToken);
        var entity = await FindAsync(userId, source, gameId, cancellationToken);
        if (entity is null)
        {
            entity = NewEntity(userId, source, gameId);
            entity.Status = status;
            await _context.UserGames.AddAsync(entity, cancellationToken);
        }
        else { entity.Status = status; entity.UpdatedAt = DateTime.UtcNow; }
        await SaveAsync(cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    public async Task<UserGameDTO?> RemoveStatusAsync(Guid userId, CatalogSource source, long gameId,
        CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");
        var entity = await FindAsync(userId, source, gameId, cancellationToken);
        if (entity is null) return null;
        entity.Status = null;
        return await SaveOrDeleteAsync(entity, cancellationToken);
    }

    public async Task<UserGameDTO?> SetFavoriteAsync(Guid userId, CatalogSource source, long gameId,
        bool isFavorite, CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");
        var entity = await FindAsync(userId, source, gameId, cancellationToken);
        if (entity is null)
        {
            if (!isFavorite) return null;
            await EnsureGameExistsAsync(source, gameId, cancellationToken);
            entity = NewEntity(userId, source, gameId);
            entity.IsFavorite = true;
            await _context.UserGames.AddAsync(entity, cancellationToken);
            await SaveAsync(cancellationToken);
            return await MapAsync(entity, cancellationToken);
        }
        entity.IsFavorite = isFavorite;
        return await SaveOrDeleteAsync(entity, cancellationToken);
    }

    public async Task<UserGameDTO?> SetWishlistAsync(Guid userId, CatalogSource source, long gameId,
        bool isWishlisted, CancellationToken cancellationToken = default)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");
        var entity = await FindAsync(userId, source, gameId, cancellationToken);
        if (entity is null)
        {
            if (!isWishlisted) return null;
            await EnsureGameExistsAsync(source, gameId, cancellationToken);
            entity = NewEntity(userId, source, gameId);
            entity.IsWishlisted = true;
            await _context.UserGames.AddAsync(entity, cancellationToken);
            await SaveAsync(cancellationToken);
            return await MapAsync(entity, cancellationToken);
        }
        entity.IsWishlisted = isWishlisted;
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

        var entity = await FindAsync(userId, source, gameId, cancellationToken);

        if (entity is null)
        {
            if (!rating.HasValue)
                return null;

            await EnsureGameExistsAsync(source, gameId, cancellationToken);
            entity = NewEntity(userId, source, gameId);
            entity.Rating = rating.Value;
            await _context.UserGames.AddAsync(entity, cancellationToken);
            await SaveAsync(cancellationToken);
            return await MapAsync(entity, cancellationToken);
        }

        entity.Rating = rating;
        return await SaveOrDeleteAsync(entity, cancellationToken);
    }

    private async Task<UserGameDTO?> SaveOrDeleteAsync(UserGame entity, CancellationToken cancellationToken)
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
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        { throw new ConflictException("Bu oyun üçün istifadəçi məlumatı artıq mövcuddur."); }
    }

    private Task<UserGame?> FindAsync(Guid userId, CatalogSource source, long gameId, CancellationToken token) =>
        _context.UserGames.FirstOrDefaultAsync(x => x.UserId == userId && x.Source == source && x.GameId == gameId, token);

    private async Task EnsureGameExistsAsync(CatalogSource source, long gameId, CancellationToken token)
    {
        gameId.EnsureValidCatalogReference(source, "Oyun");
        if (source == CatalogSource.Local)
        {
            if (gameId > int.MaxValue || !await _context.Games.AnyAsync(x => x.Id == (int)gameId, token))
                throw new NotFoundException("Oyun tapılmadı.");
            return;
        }
        if (source == CatalogSource.Igdb)
        {
            _ = await _igdbClient.GetGameByIdAsync(gameId, token) ?? throw new NotFoundException("IGDB oyunu tapılmadı.");
            return;
        }
        throw new BadRequestException("Oyun mənbəyi yanlışdır.");
    }

    private async Task<IReadOnlyList<UserGameDTO>> MapManyAsync(List<UserGame> entities, CancellationToken token)
    {
        var localIds = entities.Where(x => x.Source == CatalogSource.Local && x.GameId <= int.MaxValue)
            .Select(x => (int)x.GameId).Distinct().ToList();
        var locals = await _context.Games.AsNoTracking().Where(x => localIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, token);
        var result = new List<UserGameDTO>(entities.Count);
        foreach (var entity in entities)
        {
            if (entity.Source == CatalogSource.Local && locals.TryGetValue((int)entity.GameId, out var local))
                result.Add(Map(entity, local.Name, local.Slug, local.CoverImageUrl, local.ReleaseDate));
            else if (entity.Source == CatalogSource.Igdb)
            {
                var game = await _igdbClient.GetGameByIdAsync(entity.GameId, token);
                if (game is not null) result.Add(Map(entity, game.Name, null, game.CoverImageUrl, game.ReleaseDate));
            }
        }
        return result;
    }

    private async Task<UserGameDTO> MapAsync(UserGame entity, CancellationToken token)
    {
        if (entity.Source == CatalogSource.Local)
        {
            var game = await _context.Games.AsNoTracking().FirstOrDefaultAsync(x => x.Id == (int)entity.GameId, token)
                ?? throw new NotFoundException("Oyun tapılmadı.");
            return Map(entity, game.Name, game.Slug, game.CoverImageUrl, game.ReleaseDate);
        }
        var igdb = await _igdbClient.GetGameByIdAsync(entity.GameId, token)
            ?? throw new NotFoundException("IGDB oyunu tapılmadı.");
        return Map(entity, igdb.Name, null, igdb.CoverImageUrl, igdb.ReleaseDate);
    }

    private static UserGameDTO Map(UserGame entity, string name, string? slug, string? cover, DateOnly? releaseDate) => new()
    {
        Source = entity.Source, GameId = entity.GameId, Name = name, Slug = slug, CoverUrl = cover,
        ReleaseDate = releaseDate, Status = entity.Status, IsFavorite = entity.IsFavorite,
        IsWishlisted = entity.IsWishlisted, Rating = entity.Rating,
        CreatedAt = entity.CreatedAt, UpdatedAt = entity.UpdatedAt
    };

    private static UserGame NewEntity(Guid userId, CatalogSource source, long gameId) => new()
    { UserId = userId, Source = source, GameId = gameId, CreatedAt = DateTime.UtcNow };
    private static bool IsEmpty(UserGame entity) =>
        entity.Status is null &&
        !entity.IsFavorite &&
        !entity.IsWishlisted &&
        !entity.Rating.HasValue;

    private static void ValidateRating(double? rating)
    {
        if (!rating.HasValue)
            return;

        var value = rating.Value;
        var isHalfStep = Math.Abs(value * 2 - Math.Round(value * 2)) < 0.000001;

        if (value < 0.5 || value > 5 || !isHalfStep)
        {
            throw new BadRequestException(
                "Reytinq 0.5 ilə 5 arasında və 0.5 addımlarla olmalıdır.");
        }
    }
}
