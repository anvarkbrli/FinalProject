using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces;

public interface IUserReportService
{
    Task<UserReportDTO> CreateAsync(
        Guid reporterUserId,
        Guid reportedUserId,
        UserReportCreateDTO dto,
        CancellationToken cancellationToken = default);

    IReadOnlyList<UserReportCategoryOptionDTO> GetCategoryOptions();
}
