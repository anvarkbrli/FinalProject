using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces;

public interface IGlobalSearchService
{
    Task<GlobalSearchResultDTO> SearchAsync(
        GlobalSearchFilterDTO filter,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);
}
