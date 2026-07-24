using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface IPostService
    {
        Task<PostDTO> CreateAsync(Guid userId,PostCreateDTO dto, CancellationToken cancellationToken = default);
        Task<PostDTO> GetByIdAsync(long postId,Guid? currentUserId, CancellationToken cancellationToken = default);
        Task<PagedResultDTO<PostDTO>> GetUserPostsAsync(
            Guid userId,
            Guid? currentUserId,
            PaginationFilterDTO pagination,
            CancellationToken cancellationToken = default);
        Task<PagedResultDTO<PostDTO>> GetFeedAsync(
            Guid currentUserId,
            PaginationFilterDTO pagination,
            CancellationToken cancellationToken = default);
        Task<PostDTO> UpdateAsync(Guid userId, long postId, PostUpdateDTO dto, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid userId, long postId, CancellationToken cancellationToken = default);
    }

}

