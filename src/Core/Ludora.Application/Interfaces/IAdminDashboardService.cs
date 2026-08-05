using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces;

public interface IAdminDashboardService
{
    Task<AdminDashboardDTO> GetAsync(CancellationToken cancellationToken = default);
}
