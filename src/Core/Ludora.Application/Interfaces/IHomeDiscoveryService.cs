using Ludora.Application.DTOs.Home;

namespace Ludora.Application.Interfaces;

public interface IHomeDiscoveryService
{
    Task<HomeDiscoveryDTO> GetAsync(
        CancellationToken cancellationToken = default);
}
