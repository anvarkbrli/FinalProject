using Ludora.Application.DTOs;
using Ludora.Application.Models;

namespace Ludora.Application.Interfaces
{
    public interface IIgdbClient
    {
        bool IsEnabled { get; }
        Task<IReadOnlyList<IgdbGameModel>> GetGamesAsync(
            GameFilterDTO filter,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbGameModel>> GetGamesAsync(
            GameFilterDTO filter,
            IReadOnlyCollection<long> excludedGameIds,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbGameModel>> GetGamesSliceAsync(
            GameFilterDTO filter,
            int offset,
            int limit,
            IReadOnlyCollection<long> excludedGameIds,
            CancellationToken cancellationToken = default);

        Task<int> GetGamesCountAsync(
            GameFilterDTO filter,
            IReadOnlyCollection<long> excludedGameIds,
            CancellationToken cancellationToken = default);

        Task<IgdbGameModel?> GetGameByIdAsync(
            long id,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbGameModel>> GetGamesByIdsAsync(
            IReadOnlyCollection<long> ids,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbGameModel>> GetGamesByIdsForCardsAsync(
            IReadOnlyCollection<long> ids,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbSteamGameMappingModel>> GetSteamGameMappingsAsync(
            IReadOnlyCollection<int> steamAppIds,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbGameModel>> SearchGamesIncludingVersionsAsync(
            string search,
            int limit = 10,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbGameModel>> GetUpcomingGamesAsync(
            DateOnly fromDate,
            DateOnly toDate,
            int limit,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbPopularityTypeModel>> GetPopularityTypesAsync(
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbPopularityPrimitiveModel>> GetPopularityPrimitivesAsync(
            long popularityTypeId,
            int limit,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbGenreModel>> GetGenresAsync(
            string? search,
            CancellationToken cancellationToken = default);

        Task<IgdbGenreModel?> GetGenreByIdAsync(
            long id,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbGenreModel>> GetGenresByIdsAsync(
            IReadOnlyCollection<long> ids,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbPlatformModel>> GetPlatformsAsync(
            string? search,
            CancellationToken cancellationToken = default);

        Task<IgdbPlatformModel?> GetPlatformByIdAsync(
            long id,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbPlatformModel>> GetPlatformsByIdsAsync(
            IReadOnlyCollection<long> ids,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbCompanyModel>> GetCompaniesAsync(
            string? search,
            CancellationToken cancellationToken = default);

        Task<IgdbCompanyModel?> GetCompanyByIdAsync(
            long id,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbCompanyModel>> GetCompaniesByIdsAsync(
            IReadOnlyCollection<long> ids,
            CancellationToken cancellationToken = default);
    }
}
