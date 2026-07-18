using Ludora.Application.DTOs;
using Ludora.Domain.Enums;


namespace Ludora.Application.Interfaces
{
    public interface IUserGameService
    {
        Task<IReadOnlyList<UserGameDTO>> GetMineAsync(
            Guid userId,
            UserGameStatus? status = null,
            bool? isFavorite = null,
            bool? isWishlisted = null,
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
