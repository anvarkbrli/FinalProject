using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces;

public interface IForumFeedService
{
    Task<ForumFeedResultDTO> GetHomeAsync(
        Guid currentUserId,
        ForumFeedFilterDTO filter,
        bool canModerate,
        CancellationToken cancellationToken = default);

    Task<ForumFeedResultDTO> GetPopularAsync(
        Guid? currentUserId,
        ForumFeedFilterDTO filter,
        bool canModerate,
        CancellationToken cancellationToken = default);

    Task<ForumFeedResultDTO> GetExploreAsync(
        Guid? currentUserId,
        ForumFeedFilterDTO filter,
        bool canModerate,
        CancellationToken cancellationToken = default);
}
