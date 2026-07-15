using Ludora.Application.DTOs;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services
{
    public class UserGameService : IUserGameService
    {
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
            {
                query = query.Where(
                    x => x.Status == status.Value);
            }

            if (isFavorite.HasValue)
            {
                query = query.Where(
                    x => x.IsFavorite == isFavorite.Value);
            }

            if (isWishlisted.HasValue)
            {
                query = query.Where(
                    x => x.IsWishlisted == isWishlisted.Value);
            }

            var userGames = await query
                .OrderByDescending(
                    x => x.UpdatedAt ?? x.CreatedAt)
                .ToListAsync(cancellationToken);

            var localGameIds = userGames
                .Where(x => x.Source == CatalogSource.Local)
                .Select(x => (int)x.GameId)
                .Distinct()
                .ToList();

            var localGames = await _context.Games
                .AsNoTracking()
                .Where(x => localGameIds.Contains(x.Id))
                .ToDictionaryAsync(
                    x => x.Id,
                    cancellationToken);

            var result = new List<UserGameDTO>();

            foreach (var userGame in userGames)
            {
                var dto = new UserGameDTO
                {
                    Source = userGame.Source,
                    GameId = userGame.GameId,
                    Status = userGame.Status,
                    IsFavorite = userGame.IsFavorite,
                    IsWishlisted = userGame.IsWishlisted,
                    CreatedAt = userGame.CreatedAt,
                    UpdatedAt = userGame.UpdatedAt
                };

                if (userGame.Source == CatalogSource.Local &&
                    localGames.TryGetValue(
                        (int)userGame.GameId,
                        out var localGame))
                {
                    dto.Name = localGame.Name;
                    dto.Slug = localGame.Slug;
                    dto.ReleaseDate = localGame.ReleaseDate;
                }

                result.Add(dto);
            }

            return result;
        }

        public async Task<UserGameDTO?> GetStateAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            CancellationToken cancellationToken = default)
        {
            gameId.EnsureValidCatalogReference(
                source,
                "Oyun");

            return await _context.UserGames
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId &&
                    x.Source == source &&
                    x.GameId == gameId)
                .Select(x => new UserGameDTO
                {
                    Source = x.Source,
                    GameId = x.GameId,
                    Status = x.Status,
                    IsFavorite = x.IsFavorite,
                    IsWishlisted = x.IsWishlisted,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<UserGameDTO> SetStatusAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            UserGameStatus status,
            CancellationToken cancellationToken = default)
        {
            if (!Enum.IsDefined(
                    typeof(UserGameStatus),
                    status))
            {
                throw new BadRequestException(
                    "Oyun statusu yanlışdır.");
            }

            await EnsureGameExistsAsync(
                source,
                gameId,
                cancellationToken);

            var entity = await FindAsync(
                userId,
                source,
                gameId,
                cancellationToken);

            if (entity is null)
            {
                entity = new UserGame
                {
                    UserId = userId,
                    Source = source,
                    GameId = gameId,
                    Status = status,
                    IsFavorite = false,
                    IsWishlisted = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.UserGames.AddAsync(
                    entity,
                    cancellationToken);
            }
            else
            {
                entity.Status = status;
                entity.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(
                cancellationToken);

            return Map(entity);
        }

        public async Task<UserGameDTO?> RemoveStatusAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            CancellationToken cancellationToken = default)
        {
            gameId.EnsureValidCatalogReference(
                source,
                "Oyun");

            var entity = await FindAsync(
                userId,
                source,
                gameId,
                cancellationToken);

            if (entity is null)
            {
                return null;
            }

            entity.Status = null;

            if (IsEmpty(entity))
            {
                _context.UserGames.Remove(entity);

                await _context.SaveChangesAsync(
                    cancellationToken);

                return null;
            }

            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(
                cancellationToken);

            return Map(entity);
        }

        public async Task<UserGameDTO?> SetFavoriteAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            bool isFavorite,
            CancellationToken cancellationToken = default)
        {
            gameId.EnsureValidCatalogReference(
                source,
                "Oyun");

            var entity = await FindAsync(
                userId,
                source,
                gameId,
                cancellationToken);

            if (entity is null)
            {
                if (!isFavorite)
                {
                    return null;
                }

                await EnsureGameExistsAsync(
                    source,
                    gameId,
                    cancellationToken);

                entity = new UserGame
                {
                    UserId = userId,
                    Source = source,
                    GameId = gameId,
                    Status = null,
                    IsFavorite = true,
                    IsWishlisted = false,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.UserGames.AddAsync(
                    entity,
                    cancellationToken);

                await _context.SaveChangesAsync(
                    cancellationToken);

                return Map(entity);
            }

            entity.IsFavorite = isFavorite;

            if (IsEmpty(entity))
            {
                _context.UserGames.Remove(entity);

                await _context.SaveChangesAsync(
                    cancellationToken);

                return null;
            }

            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(
                cancellationToken);

            return Map(entity);
        }

        public async Task<UserGameDTO?> SetWishlistAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            bool isWishlisted,
            CancellationToken cancellationToken = default)
        {
            gameId.EnsureValidCatalogReference(
                source,
                "Oyun");

            var entity = await FindAsync(
                userId,
                source,
                gameId,
                cancellationToken);

            if (entity is null)
            {
                if (!isWishlisted)
                {
                    return null;
                }

                await EnsureGameExistsAsync(
                    source,
                    gameId,
                    cancellationToken);

                entity = new UserGame
                {
                    UserId = userId,
                    Source = source,
                    GameId = gameId,
                    Status = null,
                    IsFavorite = false,
                    IsWishlisted = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.UserGames.AddAsync(
                    entity,
                    cancellationToken);

                await _context.SaveChangesAsync(
                    cancellationToken);

                return Map(entity);
            }

            entity.IsWishlisted = isWishlisted;

            if (IsEmpty(entity))
            {
                _context.UserGames.Remove(entity);

                await _context.SaveChangesAsync(
                    cancellationToken);

                return null;
            }

            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(
                cancellationToken);

            return Map(entity);
        }

        private async Task<UserGame?> FindAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            CancellationToken cancellationToken)
        {
            return await _context.UserGames
                .FirstOrDefaultAsync(
                    x =>
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
            gameId.EnsureValidCatalogReference(
                source,
                "Oyun");

            if (source == CatalogSource.Local)
            {
                if (gameId > int.MaxValue)
                {
                    throw new BadRequestException(
                        "Lokal oyun ID-si yanlışdır.");
                }

                var localGameId = (int)gameId;

                var exists = await _context.Games
                    .AnyAsync(
                        x => x.Id == localGameId,
                        cancellationToken);

                if (!exists)
                {
                    throw new NotFoundException(
                        "Oyun tapılmadı.");
                }

                return;
            }

            if (source == CatalogSource.Igdb)
            {
                _ = await _igdbClient.GetGameByIdAsync(
                        gameId,
                        cancellationToken)
                    ?? throw new NotFoundException(
                        "IGDB oyunu tapılmadı.");

                return;
            }

            throw new BadRequestException(
                "Oyun mənbəyi yanlışdır.");
        }

        private static bool IsEmpty(
            UserGame entity)
        {
            return entity.Status is null &&
                   !entity.IsFavorite &&
                   !entity.IsWishlisted;
        }

        private static UserGameDTO Map(
            UserGame entity)
        {
            return new UserGameDTO
            {
                Source = entity.Source,
                GameId = entity.GameId,
                Status = entity.Status,
                IsFavorite = entity.IsFavorite,
                IsWishlisted = entity.IsWishlisted,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}
