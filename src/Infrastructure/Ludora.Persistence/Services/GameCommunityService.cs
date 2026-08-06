using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Game;
using Ludora.Application.Exceptions;
using Ludora.Application.Extensions;
using Ludora.Application.Interfaces;
using Ludora.Domain.Entities;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;

using Microsoft.EntityFrameworkCore;

namespace Ludora.Persistence.Services
{
    public sealed class GameCommunityService : IGameCommunityService
    {
        private const int MaxPageSize = 100;

        private readonly AppDbContext _context;
        private readonly IIgdbClient _igdbClient;

        public GameCommunityService(
            AppDbContext context,
            IIgdbClient igdbClient)
        {
            _context = context;
            _igdbClient = igdbClient;
        }

        public async Task<PagedResultDTO<GameCommunityUserDTO>> GetCategoryUsersAsync(
            CatalogSource source,
            long gameId,
            GameCommunityUserCategory category,
            GameCommunityUsersFilterDTO filter,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);

            gameId.EnsureValidCatalogReference(source, "Oyun");
            category.EnsureDefined("İstifadəçi kateqoriyası");
            ValidatePagination(filter.PageNumber, filter.PageSize);

            await EnsureGameExistsAsync(
                source,
                gameId,
                cancellationToken);

            var query = CreateGameUsersQuery(source, gameId);

            query = category switch
            {
                GameCommunityUserCategory.Played => query.Where(
                    userGame => userGame.Status == UserGameStatus.Played),

                GameCommunityUserCategory.Playing => query.Where(
                    userGame => userGame.Status == UserGameStatus.Playing),

                GameCommunityUserCategory.PlanToPlay => query.Where(
                    userGame => userGame.Status == UserGameStatus.PlanToPlay),

                GameCommunityUserCategory.Wishlist => query.Where(
                    userGame => userGame.IsWishlisted),

                GameCommunityUserCategory.Favorite => query.Where(
                    userGame => userGame.IsFavorite),

                GameCommunityUserCategory.Dropped => query.Where(
                    userGame => userGame.Status == UserGameStatus.Dropped),

                _ => throw new BadRequestException(
                    "İstifadəçi kateqoriyası düzgün deyil.")
            };

            return await CreatePagedResultAsync(
                query,
                filter.PageNumber,
                filter.PageSize,
                cancellationToken);
        }

        public async Task<PagedResultDTO<GameCommunityUserDTO>> GetRatingUsersAsync(
            CatalogSource source,
            long gameId,
            double rating,
            GameCommunityUsersFilterDTO filter,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filter);

            gameId.EnsureValidCatalogReference(source, "Oyun");
            ValidateRating(rating);
            ValidatePagination(filter.PageNumber, filter.PageSize);

            await EnsureGameExistsAsync(
                source,
                gameId,
                cancellationToken);

            var query = CreateGameUsersQuery(source, gameId)
                .Where(userGame => userGame.Rating == rating);

            return await CreatePagedResultAsync(
                query,
                filter.PageNumber,
                filter.PageSize,
                cancellationToken);
        }

        public async Task<IReadOnlyList<GameCommunityUserDTO>> GetWishlistPreviewAsync(
            CatalogSource source,
            long gameId,
            Guid currentUserId,
            int limit = 3,
            CancellationToken cancellationToken = default)
        {
            gameId.EnsureValidCatalogReference(source, "Oyun");

            if (limit < 1 || limit > 10)
                limit = 3;

            await EnsureGameExistsAsync(source, gameId, cancellationToken);

            var connectionIds = await _context.UserFollows
                .AsNoTracking()
                .Where(x =>
                    x.Status == FollowStatus.Accepted &&
                    (x.FollowerId == currentUserId || x.FollowingId == currentUserId))
                .Select(x => x.FollowerId == currentUserId ? x.FollowingId : x.FollowerId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (connectionIds.Count == 0)
                return [];

            return await _context.UserGames
                .AsNoTracking()
                .Where(userGame =>
                    connectionIds.Contains(userGame.UserId) &&
                    userGame.Source == source &&
                    userGame.GameId == gameId &&
                    userGame.IsWishlisted)
                .OrderByDescending(userGame => userGame.UpdatedAt ?? userGame.CreatedAt)
                .ThenBy(userGame => userGame.User.UserName)
                .Take(limit)
                .Select(userGame => new GameCommunityUserDTO
                {
                    Id = userGame.UserId,
                    UserName = userGame.User.UserName ?? string.Empty,
                    ProfileImageUrl = userGame.User.ProfileImageUrl
                })
                .ToListAsync(cancellationToken);
        }

        private IQueryable<UserGame> CreateGameUsersQuery(
            CatalogSource source,
            long gameId)
        {
            return _context.UserGames
                .AsNoTracking()
                .Where(userGame =>
                    userGame.Source == source &&
                    userGame.GameId == gameId);
        }

        private static async Task<PagedResultDTO<GameCommunityUserDTO>>
            CreatePagedResultAsync(
                IQueryable<UserGame> query,
                int pageNumber,
                int pageSize,
                CancellationToken cancellationToken)
        {
            var totalCount = await query.CountAsync(cancellationToken);
            var skip = (pageNumber - 1) * pageSize;

            var users = await query
                .OrderByDescending(userGame =>
                    userGame.UpdatedAt ?? userGame.CreatedAt)
                .ThenBy(userGame => userGame.User.UserName)
                .Skip(skip)
                .Take(pageSize)
                .Select(userGame => new GameCommunityUserDTO
                {
                    Id = userGame.UserId,
                    UserName = userGame.User.UserName ?? string.Empty,
                    ProfileImageUrl = userGame.User.ProfileImageUrl
                })
                .ToListAsync(cancellationToken);

            return new PagedResultDTO<GameCommunityUserDTO>
            {
                Items = users,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalCount == 0
                    ? 0
                    : (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        private async Task EnsureGameExistsAsync(
            CatalogSource source,
            long gameId,
            CancellationToken cancellationToken)
        {
            if (source == CatalogSource.Local)
            {
                var exists = await _context.Games
                    .AsNoTracking()
                    .AnyAsync(
                        game => game.Id == (int)gameId,
                        cancellationToken);

                if (!exists)
                {
                    throw new NotFoundException("Oyun tapılmadı.");
                }

                return;
            }

            var isHidden = await _context.IgdbGameOverrides
                .AsNoTracking()
                .AnyAsync(
                    gameOverride =>
                        gameOverride.IgdbGameId == gameId &&
                        gameOverride.IsHidden,
                    cancellationToken);

            if (isHidden)
            {
                throw new NotFoundException("Oyun tapılmadı.");
            }

            _ = await _igdbClient.GetGameByIdAsync(
                gameId,
                cancellationToken)
                ?? throw new NotFoundException(
                    "IGDB oyunu tapılmadı.");
        }

        private static void ValidatePagination(
            int pageNumber,
            int pageSize)
        {
            if (pageNumber < 1)
            {
                throw new BadRequestException(
                    "Səhifə nömrəsi 1-dən kiçik ola bilməz.");
            }

            if (pageSize is < 1 or > MaxPageSize)
            {
                throw new BadRequestException(
                    $"Səhifə ölçüsü 1-{MaxPageSize} aralığında olmalıdır.");
            }

            if ((long)(pageNumber - 1) * pageSize > int.MaxValue)
            {
                throw new BadRequestException(
                    "Səhifə parametrləri həddindən artıq böyükdür.");
            }
        }

        private static void ValidateRating(double rating)
        {
            var isHalfStep = Math.Abs(
                rating * 2 - Math.Round(rating * 2)) < 0.000001;

            if (rating < 0.5 || rating > 5 || !isHalfStep)
            {
                throw new BadRequestException(
                    "Reytinq 0.5 ilə 5 arasında və 0.5 addımlarla olmalıdır.");
            }
        }
    }
}
