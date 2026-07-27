using Ludora.Application.DTOs.FpsEstimator;

namespace Ludora.Application.Interfaces;

public interface IFpsEstimatorService
{
    Task<FpsEstimateResponseDTO> EstimateAsync(
        Guid userId,
        FpsEstimateRequestDTO request,
        CancellationToken cancellationToken = default);
}
