using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface IPostService
    {
        Task<PostDTO> CreateAsync(Guid userId,PostCreateDTO dto, CancellationToken cancellationToken = default);
        Task<PostDTO> GetByIdAsync(long postId,Guid? currentUserId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PostDTO>> GetUserPostsAsync(Guid userId, Guid? currentUserId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PostDTO>> GetFeedAsync(Guid currentUserId, CancellationToken cancellationToken = default);
        Task<PostDTO> UpdateAsync(Guid userId, long postId, PostUpdateDTO dto, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid userId, long postId, CancellationToken cancellationToken = default);
    }

}


