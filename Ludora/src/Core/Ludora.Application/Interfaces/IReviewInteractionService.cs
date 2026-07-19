using Ludora.Application.DTOs;

namespace Ludora.Application.Interfaces;

public interface IReviewInteractionService
{
    Task LikeAsync(long reviewId, Guid userId, CancellationToken cancellationToken = default);
    Task UnlikeAsync(long reviewId, Guid userId, CancellationToken cancellationToken = default);
    Task<ReviewCommentDTO> CreateCommentAsync(long reviewId, Guid userId, ReviewCommentCreateDTO dto, CancellationToken cancellationToken = default);
    Task<PagedResultDTO<ReviewCommentDTO>> GetCommentsAsync(long reviewId, ReviewCommentFilterDTO filter, Guid? currentUserId, CancellationToken cancellationToken = default);
    Task<ReviewCommentDTO> UpdateCommentAsync(long commentId, Guid userId, ReviewCommentUpdateDTO dto, CancellationToken cancellationToken = default);
    Task DeleteCommentAsync(long commentId, Guid userId, CancellationToken cancellationToken = default);
}
