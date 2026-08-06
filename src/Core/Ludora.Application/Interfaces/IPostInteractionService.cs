using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces
{
    public interface IPostInteractionService
    {
        Task LikeAsync(long postId, Guid userId, CancellationToken cancellationToken = default);
        Task UnlikeAsync(long postId, Guid userId, CancellationToken cancellationToken = default);
        Task<PagedResultDTO<InteractionLikeUserDTO>> GetLikesAsync(long postId, int pageNumber, int pageSize, Guid? currentUserId, CancellationToken cancellationToken = default);
        Task<PostCommentDTO> CreateCommentAsync(long postId, Guid userId, InteractionCommentCreateDTO dto, CancellationToken cancellationToken = default);
        Task<PagedResultDTO<PostCommentDTO>> GetCommentsAsync(long postId, InteractionCommentFilterDTO filter, Guid? currentUserId, CancellationToken cancellationToken = default);
        Task<PostCommentDTO> UpdateCommentAsync(long commentId, Guid userId, InteractionCommentUpdateDTO dto, CancellationToken cancellationToken = default);
        Task DeleteCommentAsync(long commentId, Guid userId, CancellationToken cancellationToken = default);
    }
}
