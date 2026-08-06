using Ludora.Application.DTOs;
using Ludora.Domain.Enums;

namespace Ludora.Application.Interfaces
{
    public interface IReviewService
    {
        Task<ReviewDTO> CreateAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            ReviewCreateDTO dto,
            CancellationToken cancellationToken = default);

        Task<ReviewDTO> GetByIdAsync(
            long reviewId,
            Guid? currentUserId,
            CancellationToken cancellationToken = default);

        Task<PagedResultDTO<ReviewDTO>> GetGameReviewsAsync(
            CatalogSource source,
            long gameId,
            ReviewFilterDTO filter,
            Guid? currentUserId,
            CancellationToken cancellationToken = default);

        Task<PagedResultDTO<ReviewDTO>> GetUserReviewsAsync(
            Guid userId,
            ReviewFilterDTO filter,
            Guid? currentUserId,
            CancellationToken cancellationToken = default);

        Task<PagedResultDTO<ReviewDTO>> GetMineAsync(
            Guid userId,
            ReviewFilterDTO filter,
            CancellationToken cancellationToken = default);

        Task<ReviewDTO?> GetMineForGameAsync(
            Guid userId,
            CatalogSource source,
            long gameId,
            CancellationToken cancellationToken = default);

        Task<ReviewDTO> UpdateAsync(
            Guid userId,
            long reviewId,
            ReviewUpdateDTO dto,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            Guid userId,
            long reviewId,
            CancellationToken cancellationToken = default);
    }
}
