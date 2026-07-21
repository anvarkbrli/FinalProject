using Ludora.Application.DTOs;
using Ludora.Application.DTOs.Game;
using Ludora.Domain.Enums;

namespace Ludora.Application.Interfaces;

public interface IGameCommunityService
{
    Task<PagedResultDTO<GameCommunityUserDTO>> GetCategoryUsersAsync(
        CatalogSource source,
        long gameId,
        GameCommunityUserCategory category,
        GameCommunityUsersFilterDTO filter,
        CancellationToken cancellationToken = default);

    Task<PagedResultDTO<GameCommunityUserDTO>> GetRatingUsersAsync(
        CatalogSource source,
        long gameId,
        double rating,
        GameCommunityUsersFilterDTO filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameCommunityUserDTO>> GetWishlistPreviewAsync(
        CatalogSource source,
        long gameId,
        Guid currentUserId,
        int limit = 3,
        CancellationToken cancellationToken = default);
}
