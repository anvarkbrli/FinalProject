using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface IUserActivityService
    {
        Task<PagedResultDTO<UserActivityDTO>> GetFeedAsync(
            Guid userId,
            UserActivityFilterDTO filter,
            CancellationToken cancellationToken = default);
    }
}
