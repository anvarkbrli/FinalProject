using Ludora.Application.DTOs;
using Ludora.Domain.Enums;

namespace Ludora.Application.Interfaces
{
    public interface IUserGameService
    {
        Task<PagedResultDTO<UserGameDTO>> GetMineAsync(
            Guid userId,
            UserGameStatus? status = null,
            bool? isFavorite = null,
            bool? isWishlisted = null,
            PaginationFilterDTO? pagination = null,
            CancellationToken cancellationToken = default);

        Task<UserGameDashboardDTO> GetDashboardAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<PagedResultDTO<UserGameDTO>> GetByUserAsync(
            Guid userId,
            UserGameFilterDTO filter,
            Guid? currentUserId,
            CancellationToken cancellationToken = default);

        Task<PagedResultDTO<UserGameDTO>> GetVisibleFavoritesAsync(
            Guid userId,
            Guid currentUserId,
            PaginationFilterDTO pagination,
            CancellationToken cancellationToken = default);

        Task<PagedResultDTO<ProfileGameEntryDTO>> GetProfileCollectionAsync(
            Guid userId,
            UserProfileGamesFilterDTO filter,
            Guid? currentUserId,
            CancellationToken cancellationToken = default);

        Task<UserPlaytimeStatisticsDTO> GetPlaytimeStatisticsAsync(
            Guid userId,
            UserPlaytimePeriod period,
            Guid? currentUserId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<WishlistedFriendDTO>> GetWishlistedFriendsAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            CancellationToken cancellationToken = default);

        Task<UserGameDTO?> GetStateAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            CancellationToken cancellationToken = default);

        Task<UserGameDTO> SetStatusAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            UserGameStatus status,
            CancellationToken cancellationToken = default);

        Task<UserGameDTO?> RemoveStatusAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            CancellationToken cancellationToken = default);

        Task<UserGameDTO?> SetFavoriteAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            bool isFavorite,
            CancellationToken cancellationToken = default);

        Task<UserGameDTO?> SetWishlistAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            bool isWishlisted,
            CancellationToken cancellationToken = default);

        Task<UserGameDTO?> SetRatingAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            double? rating,
            CancellationToken cancellationToken = default);
    }
}
