using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces;

public interface IGlobalSearchService
{
    Task<GlobalSearchResultDTO> SearchAsync(
        GlobalSearchFilterDTO filter,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<PagedResultDTO<SearchGameResultDTO>> SearchGamesPageAsync(
        SearchPageFilterDTO filter,
        CancellationToken cancellationToken = default);

    Task<PagedResultDTO<SearchUserResultDTO>> SearchUsersPageAsync(
        SearchPageFilterDTO filter,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<PagedResultDTO<SearchDiscussionResultDTO>> SearchDiscussionsPageAsync(
        SearchPageFilterDTO filter,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<PagedResultDTO<SearchReviewResultDTO>> SearchReviewsPageAsync(
        SearchPageFilterDTO filter,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<PagedResultDTO<SearchGameListResultDTO>> SearchListsPageAsync(
        SearchPageFilterDTO filter,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);
}
