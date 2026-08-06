using Ludora.Application.DTOs.Home;

namespace Ludora.Application.Interfaces
{
    public interface IHomeSocialService
    {
        Task<HomeSocialDashboardDTO> GetAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
    }
}
