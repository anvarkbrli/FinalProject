using Ludora.Application.DTOs.Home;

namespace Ludora.Application.Interfaces;

public interface IHomeInterestRecommendationService
{
    Task<HomeInterestRecommendationsDTO> GetAsync(
        Guid userId,
        string language,
        CancellationToken cancellationToken = default);
}
