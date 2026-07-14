

using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{

    public interface IIgdbClient
    {
        Task<IReadOnlyList<IgdbGameModel>> GetGamesAsync(
            GameFilterDTO filter,
            CancellationToken cancellationToken = default);

        Task<IgdbGameModel?> GetGameByIdAsync(long id,CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbGameModel>> GetGamesByIdsAsync(IReadOnlyCollection<long> ids,CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbGenreModel>> GetGenresAsync(string? search,CancellationToken cancellationToken = default);

        Task<IgdbGenreModel?> GetGenreByIdAsync(long id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbPlatformModel>> GetPlatformsAsync(string? search,CancellationToken cancellationToken = default);

        Task<IgdbPlatformModel?> GetPlatformByIdAsync(long id,CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IgdbCompanyModel>> GetCompaniesAsync(string? search,CancellationToken cancellationToken = default);

        Task<IgdbCompanyModel?> GetCompanyByIdAsync(long id,CancellationToken cancellationToken = default);
    }
}
